namespace MURO.Domain.Entities;

/// <summary>
/// Satılabilir paket — bir veya birden fazla gruba erişim verir.
/// Süre paketin kendisinde tanımlanır; 0 = sınırsız.
/// </summary>
public class Package
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }

    /// <summary>
    /// Kaç gün erişim verilecek. 0 = sınırsız.
    /// Satış sitesinden gelen webhook bu değeri kullanır.
    /// Admin UserPackage üzerinden override edebilir.
    /// </summary>
    public int DurationDays { get; set; } = 0;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<PackageGroup> PackageGroups { get; set; } = new List<PackageGroup>();
    public ICollection<UserPackage> UserPackages { get; set; } = new List<UserPackage>();
}
