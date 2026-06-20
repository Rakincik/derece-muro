using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Services;

/// <summary>
/// BigBlueButton REST API ile iletişim kurar.
/// Tüm istekler SHA-256 checksum ile imzalanır:
/// checksum = SHA256(methodName + queryString + sharedSecret)
/// </summary>
public class BbbService : IBbbService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<BbbService> _logger;
    private readonly ICacheService _cache;

    // Config kısayolları
    private string BbbUrl => _config["Bbb:Url"]
        ?? throw new InvalidOperationException("Bbb:Url yapılandırılmamış.");
    private string BbbSecret => _config["Bbb:Secret"]
        ?? throw new InvalidOperationException("Bbb:Secret yapılandırılmamış.");

    public BbbService(HttpClient httpClient, IConfiguration config, ILogger<BbbService> logger, ICacheService cache)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _cache = cache;
    }

    // ─── CreateMeeting ─────────────────────────────────────────────────────────

    public async Task<string> CreateMeetingAsync(BbbMeetingOptions options)
    {
        var parameters = new Dictionary<string, string>
        {
            ["name"]             = options.MeetingName,
            ["meetingID"]        = options.MeetingId,
            ["attendeePW"]       = options.AttendeePw,
            ["moderatorPW"]      = options.ModeratorPw,
            // Kayıt
            ["record"]           = options.RecordingEnabled ? "true" : "false",
            ["autoStartRecording"] = "false",
            ["allowStartStopRecording"] = options.RecordingEnabled ? "true" : "false",
            ["recordFullDurationMedia"] = "false",
            ["notifyRecordingIsOn"] = options.RecordingEnabled ? "true" : "false",
            // Ses & Mikrofon
            ["muteOnStart"]      = "true",
            ["allowModsToUnmuteUsers"] = "true",
            ["lockSettingsDisableMic"] = "true",
            // Chat & İletişim
            ["lockSettingsDisablePrivateChat"] = "true",
            ["lockSettingsDisablePublicChat"]  = "false",
            // Kamera
            ["allowModsToEjectCameras"] = "true",
            ["lockSettingsDisableCam"] = "true",
            ["meetingCameraCap"] = _config["Bbb:Defaults:MeetingCameraCap"] ?? "20",
            // Güvenlik & Layout
            ["lockSettingsLockedLayout"] = "true",
            ["lockSettingsLockOnJoin"]   = "true",
            ["guestPolicy"]      = "ALWAYS_ACCEPT", // Öğrenciler onay beklemeden direkt girsin
            ["meetingLayout"]    = "CUSTOM_LAYOUT",
            
            // Kapasite & Süre
            ["maxParticipants"]  = _config["Bbb:Defaults:MaxParticipants"] ?? "200",
            ["endWhenNoModerator"] = "true",
            ["endWhenNoModeratorDelayInMinutes"] = "5",
        };

        if (options.WelcomeMessage != null)
            parameters["welcome"] = options.WelcomeMessage;

        if (options.DurationMinutes.HasValue)
            parameters["duration"] = options.DurationMinutes.Value.ToString();

        if (options.LogoutURL != null)
            parameters["logoutURL"] = options.LogoutURL;

        // Metadata — kayıt eşleştirme ve callback
        if (options.SessionId != null)
            parameters["meta_sessionId"] = options.SessionId;
        if (options.CourseId != null)
            parameters["meta_courseId"] = options.CourseId;


        var url = BuildUrl("create", parameters);

        _logger.LogInformation("BBB CreateMeeting → URL: {Url}", url);

        try
        {
            // Varsayılan sunumu 16:9 oranında boş bir PDF ile değiştiriyoruz (Geniş beyaz ekran)
            var xmlPayload = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<modules>
  <module name=""presentation"">
    <document name=""blank.pdf"">JVBERi0xLjQKMSAwIG9iaiA8PC9UeXBlL0NhdGFsb2cvUGFnZXMgMiAwIFI+PiBlbmRvYmoKMiAwIG9iaiA8PC9UeXBlL1BhZ2VzL0NvdW50IDEvS2lkc1szIDAgUl0+PiBlbmRvYmoKMyAwIG9iaiA8PC9UeXBlL1BhZ2UvUGFyZW50IDIgMCBSL01lZGlhQm94WzAgMCAxOTIwIDEwODBdPj4gZW5kb2JqCnhyZWYKMCA0CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAwOSAwMDAwMCBuIAowMDAwMDAwMDU0IDAwMDAwIG4gCjAwMDAwMDAxMDUgMDAwMDAgbiAKdHJhaWxlciA8PC9TaXplIDQvUm9vdCAxIDAgUj4+CnN0YXJ0eHJlZgoxNzgKJSVFT0YK</document>
  </module>
</modules>";
            var content = new StringContent(xmlPayload, System.Text.Encoding.UTF8, "application/xml");
            
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var data = DeserializeXml<BbbCreateMeetingResponse>(stream);

            if (data.ReturnCode != "SUCCESS")
            {
                var message = data.Message ?? "Bilinmeyen hata";
                _logger.LogError("BBB CreateMeeting BAŞARISIZ: {Message}", message);
                throw new InvalidOperationException($"BBB meeting oluşturulamadı: {message}");
            }

            var meetingId = data.MeetingId ?? options.MeetingId;
            _logger.LogInformation("BBB meeting oluşturuldu ✓ MeetingID: {Id}", meetingId);
            return meetingId;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "BBB sunucusuna bağlanılamadı.");
            throw new InvalidOperationException("BBB sunucusuna ulaşılamıyor. Sunucunun çalıştığından emin olun.", ex);
        }
    }

    // ─── GetJoinUrl ────────────────────────────────────────────────────────────

    public Task<string> GetJoinUrlAsync(BbbJoinOptions options)
    {
        var parameters = new Dictionary<string, string>
        {
            ["fullName"]  = options.FullName,
            ["meetingID"] = options.MeetingId,
            ["password"]  = options.Password,
            ["userID"]    = options.UserId.ToString(),
            ["redirect"]  = "true",
        };

        if (!string.IsNullOrEmpty(options.LogoutUrl))
        {
            parameters["userdata-bbb_custom_logoutURL"] = options.LogoutUrl;
        }

        // Moderatör ekstra yetkiler alır
        if (options.IsModerator)
        {
            parameters["joinViaHtml5"] = "true";
        }
        else
        {
            // Öğrenciler (Moderatör olmayanlar) için ses testini atla ve sadece dinleyici olarak başlat
            parameters["userdata-bbb_skip_check_audio"] = "true";
            parameters["userdata-bbb_listen_only_mode"] = "true";
            // Kamerasını otomatik açmasın (Güvenlik)
            parameters["userdata-bbb_auto_share_webcam"] = "false";
        }

        var url = BuildUrl("join", parameters);

        _logger.LogInformation("BBB JoinUrl üretildi → User:{UserId} Meeting:{MeetingId} Mod:{IsMod}",
            options.UserId, options.MeetingId, options.IsModerator);

        // Join için URL doğrudan döner (server'a GET endPoint'i açılmaz, frontend redirect eder)
        return Task.FromResult(url);
    }

    // ─── EndMeeting ────────────────────────────────────────────────────────────

    public async Task<bool> EndMeetingAsync(string meetingId, string moderatorPw)
    {
        var parameters = new Dictionary<string, string>
        {
            ["meetingID"] = meetingId,
            ["password"]  = moderatorPw,
        };

        var url = BuildUrl("end", parameters);

        _logger.LogInformation("BBB EndMeeting → {MeetingId}", meetingId);

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var data = DeserializeXml<BbbBaseResponse>(stream);

            var success = data.ReturnCode == "SUCCESS";

            if (!success)
            {
                var message = data.Message ?? "Bilinmeyen hata";
                _logger.LogWarning("BBB EndMeeting BAŞARISIZ: {Message}", message);
            }
            else
            {
                _logger.LogInformation("BBB meeting sonlandırıldı ✓ {MeetingId}", meetingId);
            }

            return success;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "BBB EndMeeting HTTP hatası: {MeetingId}", meetingId);
            return false;
        }
    }

    // ─── GetRecordings ─────────────────────────────────────────────────────────

    public async Task<List<BbbRecordingInfo>> GetRecordingsAsync(string meetingId)
    {
        var parameters = new Dictionary<string, string>
        {
            ["meetingID"] = meetingId,
        };

        var url = BuildUrl("getRecordings", parameters);

        _logger.LogInformation("BBB GetRecordings → {MeetingId}", meetingId);

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var data = DeserializeXml<BbbGetRecordingsResponse>(stream);

            if (data.ReturnCode != "SUCCESS" || data.Recordings == null)
                return new List<BbbRecordingInfo>();

            var recordings = data.Recordings
                .Select(r => new BbbRecordingInfo(
                    RecordingId: r.RecordId ?? "",
                    MeetingId: r.MeetingId ?? meetingId,
                    PlaybackUrl: r.PlaybackFormats?.FirstOrDefault()?.Url,
                    DurationSeconds: ParseRecordingDuration(r),
                    StartTime: ParseUnixTimestamp(r.StartTime),
                    Status: r.State ?? "unknown"
                ))
                .ToList();

            _logger.LogInformation("BBB GetRecordings → {Count} kayıt bulundu", recordings.Count);
            return recordings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BBB GetRecordings hatası: {MeetingId}", meetingId);
            return new List<BbbRecordingInfo>();
        }
    }

    // ─── IsMeetingRunning ──────────────────────────────────────────────────────

    public async Task<bool> IsMeetingRunningAsync(string meetingId)
    {
        var cacheKey = $"bbb:meeting_running:{meetingId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var parameters = new Dictionary<string, string>
            {
                ["meetingID"] = meetingId,
            };

            var url = BuildUrl("isMeetingRunning", parameters);

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                var data = DeserializeXml<BbbIsMeetingRunningResponse>(stream);
                
                return data.ReturnCode == "SUCCESS" && data.Running == "true";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BBB IsMeetingRunning hatası: {MeetingId}", meetingId);
                return false;
            }
        }, TimeSpan.FromSeconds(15)); // Canlı ders durumunu 15 saniye önbellekte tut (API Şelalesi engeli)
    }

    // ─── Yardımcı Metodlar ─────────────────────────────────────────────────────

    // ═══════════════════════════════════════════════════════════════════
    // MONITORING — Admin platform izleme
    // ═══════════════════════════════════════════════════════════════════

    public async Task<BbbMeetingDetail?> GetMeetingInfoAsync(string meetingId)
    {
        var parameters = new Dictionary<string, string>
        {
            ["meetingID"] = meetingId,
        };
        var url = BuildUrl("getMeetingInfo", parameters);

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var data = DeserializeXml<BbbGetMeetingInfoResponse>(stream);

            if (data.ReturnCode != "SUCCESS")
                return null;

            return MapToMeetingDetail(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BBB GetMeetingInfo hatası: {MeetingId}", meetingId);
            return null;
        }
    }

    public async Task<List<BbbMeetingDetail>> GetMeetingsAsync()
    {
        var url = BuildUrl("getMeetings", new Dictionary<string, string>());
        var results = new List<BbbMeetingDetail>();

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var data = DeserializeXml<BbbGetMeetingsResponse>(stream);

            if (data.ReturnCode != "SUCCESS" || data.Meetings == null)
                return results;

            foreach (var m in data.Meetings)
            {
                results.Add(MapToMeetingDetail(m));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BBB GetMeetings hatası");
        }

        return results;
    }

    public async Task<bool> ForceEndMeetingAsync(string meetingId)
    {
        // getMeetingInfo'dan moderator password'ü al
        var info = await GetMeetingInfoAsync(meetingId);
        if (info == null || !info.IsRunning)
            return false;

        // EndMeeting ile bitir (password'ü BBB'den aldık)
        var parameters = new Dictionary<string, string>
        {
            ["meetingID"] = meetingId,
            ["password"] = "mp", // fallback default mod password
        };
        var url = BuildUrl("end", parameters);

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var data = DeserializeXml<BbbBaseResponse>(stream);
            
            return data.ReturnCode == "SUCCESS";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BBB ForceEndMeeting hatası: {MeetingId}", meetingId);
            return false;
        }
    }

    // ── XML Mappers ────────────────────────────────────────────────────────────

    private BbbMeetingDetail MapToMeetingDetail(BbbXmlMeetingInfo data)
    {
        var attendees = data.Attendees?.Select(a => new BbbParticipant
        {
            FullName = a.FullName ?? "",
            Role = a.Role ?? "VIEWER",
            IsPresenter = a.IsPresenter == "true",
            HasJoinedVoice = a.HasJoinedVoice == "true",
            HasVideo = a.HasVideo == "true",
            ClientType = a.ClientType ?? "HTML5",
        }).ToList() ?? new List<BbbParticipant>();

        var breakoutRooms = data.BreakoutRooms?.Select(b => new BbbBreakoutRoom
        {
            MeetingId = b.MeetingId ?? "",
            Name = b.Name ?? "",
            ParticipantCount = b.ParticipantCount,
        }).ToList() ?? new List<BbbBreakoutRoom>();

        return new BbbMeetingDetail
        {
            MeetingId = data.MeetingId ?? "",
            MeetingName = data.MeetingName ?? "",
            IsRunning = data.Running == "true",
            ParticipantCount = data.ParticipantCount,
            ModeratorCount = data.ModeratorCount,
            ListenerCount = data.ListenerCount,
            VideoCount = data.VideoCount,
            VoiceParticipantCount = data.VoiceParticipantCount,
            IsRecording = data.IsRecording == "true",
            IsBreakout = data.IsBreakout == "true",
            StartTime = ParseUnixTimestamp(data.StartTime),
            Duration = ParseDuration(data.Duration),
            MaxUsers = data.MaxUsers,
            Participants = attendees,
            BreakoutRooms = breakoutRooms,
        };
    }

    private T DeserializeXml<T>(Stream stream)
    {
        var serializer = new XmlSerializer(typeof(T));
        return (T)serializer.Deserialize(stream)!;
    }

    // ── Genel Yardımcılar ──────────────────────────────────────────────

    /// <summary>
    /// BBB API URL'i oluşturur ve SHA-256 checksum ile imzalar.
    /// Format: {BbbUrl}/{method}?{queryString}&checksum={sha256hash}
    /// </summary>
    private string BuildUrl(string method, Dictionary<string, string> parameters)
    {
        // Query string'i URI encoding ile oluştur — BBB %20 bekler, + değil
        var queryString = string.Join("&",
            parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        // Checksum: SHA256(method + queryString + secret)
        var dataToHash = method + queryString + BbbSecret;
        var checksum = ComputeSha256(dataToHash);

        return $"{BbbUrl}/{method}?{queryString}&checksum={checksum}";
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int ParseDuration(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        // BBB duration milisaniye cinsinden gelir
        if (long.TryParse(value, out var ms))
            return (int)(ms / 1000);
        return 0;
    }

    private static int ParseRecordingDuration(BbbXmlRecording r)
    {
        // format.length is usually in minutes
        var format = r.PlaybackFormats?.FirstOrDefault();
        if (format != null && !string.IsNullOrEmpty(format.Length) && int.TryParse(format.Length, out var minutes) && minutes > 0)
        {
            return minutes * 60;
        }
        return ParseDuration(r.Duration);
    }

    private static DateTime ParseUnixTimestamp(string? value)
    {
        if (string.IsNullOrEmpty(value)) return DateTime.UtcNow;
        if (long.TryParse(value, out var ms))
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        return DateTime.UtcNow;
    }
}
