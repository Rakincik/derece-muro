namespace MURO.Application.Interfaces;

/// <summary>
/// Sistem sağlık ve metrik servisi — VEP Control Plane entegrasyonu için.
/// </summary>
public interface ISystemHealthService
{
    /// <summary>
    /// Tüm servislerin (DB, Redis, BBB, Worker, Storage) detaylı sağlık durumunu döner.
    /// </summary>
    Task<SystemHealthReport> GetHealthReportAsync();

    /// <summary>
    /// Hizmetin trafik alıp alamayacağını (DB, Redis) hızlıca kontrol eder.
    /// </summary>
    Task<bool> CheckReadinessAsync();
}

// ── Health Report DTO'ları ──────────────────────────────────────────

public class SystemHealthReport
{
    public string OverallStatus { get; set; } = "unknown"; // healthy, degraded, unhealthy
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = "";
    public long UptimeSeconds { get; set; }

    public ServiceHealth Api { get; set; } = new();
    public ServiceHealth Database { get; set; } = new();
    public ServiceHealth Redis { get; set; } = new();
    public ServiceHealth Bbb { get; set; } = new();
    public ServiceHealth Worker { get; set; } = new();
    public StorageHealth Storage { get; set; } = new();
}

public class ServiceHealth
{
    public string Status { get; set; } = "unknown"; // ok, error, timeout, disabled
    public int ResponseMs { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

public class StorageHealth
{
    public string Status { get; set; } = "ok";
    public int TotalMediaAssets { get; set; }
    public int TotalRecordings { get; set; }
    public int PendingRecordings { get; set; }
}
