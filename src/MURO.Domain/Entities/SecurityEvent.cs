namespace MURO.Domain.Entities;

/// <summary>
/// Güvenlik olaylarının audit logu.
/// EventType: LOGIN_SUCCESS, LOGIN_FAILED, SESSION_KICKED, NEW_IP_LOGIN, BRUTE_FORCE_DETECTED
/// </summary>
public class SecurityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }        // Null = email bulunamadı (brute-force vb.)
    public Guid? TenantId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Details { get; set; }     // JSON: önceki IP, kicked sessionId vb.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation (optional — userId null olabilir)
    public User? User { get; set; }
}
