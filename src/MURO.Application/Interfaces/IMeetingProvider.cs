namespace MURO.Application.Interfaces;

/// <summary>
/// Canlı ders platformu soyutlaması — BBB veya Jitsi.
/// Tenant ayarlarına göre hangi provider seçileceğine MeetingProviderFactory karar verir.
/// </summary>
public interface IMeetingProvider
{
    /// <summary>Provider adı (bilgi amaçlı)</summary>
    string ProviderName { get; }

    /// <summary>Yeni bir canlı ders odası oluşturur ve oda ID'sini döner.</summary>
    Task<MeetingCreateResult> CreateMeetingAsync(CreateMeetingRequest request);

    /// <summary>Kullanıcı için odaya katılma URL'i üretir.</summary>
    Task<string> GetJoinUrlAsync(JoinMeetingRequest request);

    /// <summary>Aktif dersi bitirir.</summary>
    Task<bool> EndMeetingAsync(string meetingId);

    /// <summary>Ders hâlâ aktif mi?</summary>
    Task<bool> IsMeetingRunningAsync(string meetingId);
}

// ─── Ortak DTO'lar ───────────────────────────────────────────────────────────

public record CreateMeetingRequest(
    string MeetingId,
    string MeetingName,
    bool RecordingEnabled = false,
    string? WelcomeMessage = null,
    int? DurationMinutes = null,
    string? LogoutUrl = null,
    string? TenantId = null
);

public record JoinMeetingRequest(
    string MeetingId,
    string FullName,
    Guid UserId,
    bool IsModerator
);

public record MeetingCreateResult(
    string MeetingId,
    string Provider,      // "bbb" veya "jitsi"
    string? JoinUrl = null // Jitsi: direkt URL döner, BBB: ayrıca GetJoinUrl gerekir
);
