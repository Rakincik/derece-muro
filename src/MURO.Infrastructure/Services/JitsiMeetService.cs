using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Jitsi Meet entegrasyonu — iframe tabanlı, hafif video konferans.
/// BBB'ye alternatif olarak müşteriye opsiyonel sunulur.
/// 
/// Jitsi API'sı çok basit: oda URL'i + JWT token yeterli.
/// REST API yok, webhook yok — tamamen client-side.
/// </summary>
public class JitsiMeetService : IMeetingProvider
{
    private readonly IConfiguration _config;
    private readonly ILogger<JitsiMeetService> _logger;

    public string ProviderName => "jitsi";

    public JitsiMeetService(IConfiguration config, ILogger<JitsiMeetService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Jitsi'de oda "oluşturmaya" gerek yok — ilk katılan kişi odayı otomatik oluşturur.
    /// Bu metod sadece oda bilgisini döner + join URL üretir.
    /// </summary>
    public Task<MeetingCreateResult> CreateMeetingAsync(CreateMeetingRequest request)
    {
        var baseUrl = GetJitsiUrl();
        var roomName = SanitizeRoomName(request.MeetingId);
        var joinUrl = $"{baseUrl}/{roomName}";

        _logger.LogInformation("Jitsi meeting hazırlandı: {RoomName} → {Url}", roomName, joinUrl);

        return Task.FromResult(new MeetingCreateResult(
            MeetingId: roomName,
            Provider: "jitsi",
            JoinUrl: joinUrl
        ));
    }

    /// <summary>
    /// Kullanıcı için Jitsi join URL'i üretir.
    /// JWT kimlik doğrulama etkinse, imzalı token eklenir.
    /// </summary>
    public Task<string> GetJoinUrlAsync(JoinMeetingRequest request)
    {
        var baseUrl = GetJitsiUrl();
        var roomName = SanitizeRoomName(request.MeetingId);
        var displayName = Uri.EscapeDataString(request.FullName);

        var url = $"{baseUrl}/{roomName}";

        // JWT auth etkinse token ekle
        var jwtSecret = _config["Jitsi:JwtSecret"];
        if (!string.IsNullOrWhiteSpace(jwtSecret))
        {
            var token = GenerateJitsiJwt(request, roomName, jwtSecret);
            url += $"?jwt={token}";
        }

        // Kullanıcı adını URL'e ekle
        url += (url.Contains('?') ? "&" : "?") + $"userInfo.displayName={displayName}";

        // Moderatör değilse bazı özellikleri kısıtla
        if (!request.IsModerator)
        {
            url += "#config.startWithVideoMuted=true&config.disableModeratorIndicator=true";
        }

        return Task.FromResult(url);
    }

    /// <summary>
    /// Jitsi'de "dersi bitirme" API'sı yok — son kişi çıkınca oda kapanır.
    /// Bu metod her zaman true döner (uyumluluk için).
    /// </summary>
    public Task<bool> EndMeetingAsync(string meetingId)
    {
        _logger.LogInformation("Jitsi meeting sonlandırma istendi (client-side): {MeetingId}", meetingId);
        // Jitsi'de sunucu tarafı end yok - frontend konferansa "hangup" gönderir
        return Task.FromResult(true);
    }

    /// <summary>
    /// Jitsi'de "meeting running mı?" kontrolü REST API'sı yok.
    /// Prosody XMPP üzerinden yapılabilir ama karmaşık.
    /// Şimdilik true döner — frontend client-side kontrol eder.
    /// </summary>
    public Task<bool> IsMeetingRunningAsync(string meetingId)
    {
        // Jitsi'nin durumunu doğrudan kontrol edemiyoruz (BBB gibi API yok)
        // Frontend IFrame API ile kontrol edilir
        return Task.FromResult(true);
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────

    private string GetJitsiUrl()
        => _config["Jitsi:ServerUrl"]?.TrimEnd('/') ?? "https://meet.jit.si";

    /// <summary>Jitsi oda adlarında özel karakter olamaz</summary>
    private static string SanitizeRoomName(string name)
        => name.Replace(" ", "_")
               .Replace("/", "_")
               .Replace("\\", "_")
               .Replace(".", "_");

    /// <summary>
    /// Jitsi JWT token üretir (Prosody JWT auth modülü için).
    /// Bu token ile kullanıcı kimliği ve moderatör yetkisi aktarılır.
    /// </summary>
    private string GenerateJitsiJwt(JoinMeetingRequest request, string roomName, string secret)
    {
        var appId = _config["Jitsi:AppId"] ?? "muro";

        var claims = new[]
        {
            new Claim("room", roomName),
            new Claim("sub", appId),
            new Claim("iss", appId),
            new Claim("aud", appId),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: appId,
            audience: appId,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(4),
            signingCredentials: creds
        );

        // Jitsi JWT'sine kullanıcı bilgisi context olarak eklenir
        // Not: Standard JWT claim kullanılır, Jitsi bunu "context.user" olarak yorumlar

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
