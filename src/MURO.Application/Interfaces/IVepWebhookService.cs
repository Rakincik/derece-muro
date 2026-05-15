namespace MURO.Application.Interfaces;

/// <summary>
/// VEP Control Plane'e olay bildirimi gönderen webhook servisi.
/// MURO'da önemli olaylar olduğunda (oturum başladı, kayıt tamamlandı vb.)
/// yapılandırılmış VEP URL'sine HTTP POST gönderir.
/// </summary>
public interface IVepWebhookService
{
    /// <summary>
    /// VEP'e olay bildirimi gönderir.
    /// </summary>
    Task NotifyAsync(VepWebhookEvent evt);

    /// <summary>
    /// VEP bağlantı durumunu kontrol eder.
    /// </summary>
    Task<bool> IsConfiguredAsync();
}

// ── Event Tipleri ──────────────────────────────────────────────────

public class VepWebhookEvent
{
    public string EventType { get; set; } = "";       // "session.started", "recording.completed", vb.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string TenantCode { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Bilinen VEP webhook event tipleri.
/// </summary>
public static class VepEventTypes
{
    // Oturum olayları
    public const string SessionStarted = "session.started";
    public const string SessionEnded = "session.ended";

    // Kayıt olayları
    public const string RecordingStarted = "recording.started";
    public const string RecordingCompleted = "recording.completed";
    public const string RecordingFailed = "recording.failed";

    // Kullanıcı olayları
    public const string UserRegistered = "user.registered";
    public const string UserPackageActivated = "user.package_activated";

    // Sistem olayları
    public const string SystemHealthChanged = "system.health_changed";
    public const string StorageThreshold = "system.storage_threshold";
}
