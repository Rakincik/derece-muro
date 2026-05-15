namespace MURO.Domain.Entities;

/// <summary>
/// Bir öğrencinin satın aldığı / aktive edilmiş paketi temsil eder.
/// ExpiresAt null ise sınırsız erişim.
/// Admin ExpiresAt'ı manuel olarak değiştirebilir (uzatma/kısaltma).
/// Süre dolunca arka plan servisi Status'u "expired" yapar.
/// </summary>
public class UserPackage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PackageId { get; set; }

    /// <summary>Satış sitesinden gelen sipariş ID'si (webhook takibi için)</summary>
    public string? OrderId { get; set; }

    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// null = sınırsız erişim.
    /// Değer varsa GroupAccessService bu tarihi kontrol eder.
    /// Admin bu alanı manuel olarak güncelleyebilir.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>active | expired | cancelled</summary>
    public string Status { get; set; } = "active";

    /// <summary>webhook | admin_manual | import</summary>
    public string Source { get; set; } = "admin_manual";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Package Package { get; set; } = null!;
}
