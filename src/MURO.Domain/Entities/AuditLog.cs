namespace MURO.Domain.Entities;

/// <summary>
/// Genel audit log — CRUD işlemlerinin kaydı.
/// SecurityEvent güvenlik olayları (login, brute-force) için kullanılır.
/// AuditLog ise entity değişiklikleri (oluşturma, güncelleme, silme) içindir.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? TenantId { get; set; }
    public string Action { get; set; } = string.Empty;  // Create, Update, Delete
    public string EntityType { get; set; } = string.Empty; // User, Course, Session, etc.
    public string? EntityId { get; set; }
    public string? EntityName { get; set; }              // İlgili entity adı (Kurs başlığı vb.)
    public string? Details { get; set; }                 // JSON: ek bilgi
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
}
