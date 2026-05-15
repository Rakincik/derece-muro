namespace MURO.Domain.Entities;

public class SessionAttendance
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }

    // BBB'den gelen giriş/çıkış damgaları
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }

    // Hesaplanan süre (dakika olarak)
    public int? DurationMinutes { get; set; }

    // Katılım durumu
    public bool IsPresent => DurationMinutes.HasValue && DurationMinutes > 0;

    // Navigation
    public Session Session { get; set; } = null!;
    public User User { get; set; } = null!;
}
