namespace MURO.Application.Interfaces;

/// <summary>
/// BigBlueButton API ile tüm iletişimi yöneten servis.
/// Her BBB çağrısı SHA-256 checksum ile imzalanır.
/// </summary>
public interface IBbbService
{
    /// <summary>
    /// BBB sunucusunda yeni bir meeting oluşturur ve BBB Meeting ID'yi döndürür.
    /// Meeting ID formatı: {TenantCode}_{SessionId}
    /// </summary>
    Task<string> CreateMeetingAsync(BbbMeetingOptions options);

    /// <summary>
    /// Kullanıcı için imzalı BBB join URL'i üretir.
    /// Kullanıcı bu URL'e yönlendirilince doğrudan odaya girer.
    /// </summary>
    Task<string> GetJoinUrlAsync(BbbJoinOptions options);

    /// <summary>
    /// BBB'de aktif olan bir dersi bitirir.
    /// Moderatör "Dersi Bitir" butonuna bastığında çağrılır.
    /// </summary>
    Task<bool> EndMeetingAsync(string meetingId, string tenantId);

    /// <summary>
    /// BBB sunucusundan bir meeting'e ait kayıtları çeker.
    /// recording-ready webhook'tan ~1-5 dk sonra kayıt hazır olur.
    /// </summary>
    Task<List<BbbRecordingInfo>> GetRecordingsAsync(string meetingId);

    /// <summary>
    /// Meeting'in BBB'de hâlâ aktif olup olmadığını kontrol eder.
    /// </summary>
    Task<bool> IsMeetingRunningAsync(string meetingId);

    /// <summary>
    /// BBB'den meeting detayını çeker: katılımcı listesi, breakout rooms, metadata.
    /// Admin monitoring için kullanılır.
    /// </summary>
    Task<BbbMeetingDetail?> GetMeetingInfoAsync(string meetingId);

    /// <summary>
    /// BBB sunucusundaki tüm aktif meetingleri listeler.
    /// Platform-wide monitoring için kullanılır.
    /// </summary>
    Task<List<BbbMeetingDetail>> GetMeetingsAsync();

    /// <summary>
    /// BBB'de aktif bir toplantıyı zorla bitirir.
    /// </summary>
    Task<bool> ForceEndMeetingAsync(string meetingId);
}

// ─── BBB DTO'ları ────────────────────────────────────────────────────────────

public record BbbMeetingOptions(
    string MeetingId,          // {TenantCode}_{SessionId}
    string MeetingName,        // Ders başlığı
    string AttendeePw,         // Öğrenci şifresi (sistem üretir)
    string ModeratorPw,        // Moderatör şifresi (sistem üretir)
    bool RecordingEnabled,
    string? WelcomeMessage = null,
    int? DurationMinutes = null,
    string? LogoutURL = null,
    string? SessionId = null,
    string? CourseId = null,
    string? TenantId = null
);

public record BbbJoinOptions(
    string MeetingId,
    string FullName,           // Kullanıcının görünen adı
    string Password,           // AttendeePw veya ModeratorPw
    Guid UserId,
    bool IsModerator
);

public record BbbRecordingInfo(
    string RecordingId,
    string MeetingId,
    string? PlaybackUrl,       // BBB'nin verdiği oynatma URL'i
    int DurationSeconds,
    DateTime StartTime,
    string Status              // "published", "processing", "unpublished"
);

// ─── Monitoring DTO'ları ─────────────────────────────────────────────────────

public record BbbMeetingDetail
{
    public string MeetingId { get; init; } = "";
    public string MeetingName { get; init; } = "";
    public bool IsRunning { get; init; }
    public int ParticipantCount { get; init; }
    public int ModeratorCount { get; init; }
    public int ListenerCount { get; init; }
    public int VideoCount { get; init; }
    public int VoiceParticipantCount { get; init; }
    public bool IsRecording { get; init; }
    public bool IsBreakout { get; init; }
    public DateTime? StartTime { get; init; }
    public long Duration { get; init; }
    public int MaxUsers { get; init; }
    public List<BbbParticipant> Participants { get; init; } = new();
    public List<BbbBreakoutRoom> BreakoutRooms { get; init; } = new();
}

public record BbbParticipant
{
    public string FullName { get; init; } = "";
    public string Role { get; init; } = ""; // MODERATOR, VIEWER
    public bool IsPresenter { get; init; }
    public bool HasJoinedVoice { get; init; }
    public bool HasVideo { get; init; }
    public string ClientType { get; init; } = ""; // FLASH, HTML5
}

public record BbbBreakoutRoom
{
    public string MeetingId { get; init; } = "";
    public string Name { get; init; } = "";
    public int ParticipantCount { get; init; }
}

