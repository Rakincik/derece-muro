using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    // — Branding & Customization —
    public string? Subdomain { get; set; }           // "abckpss" → abckpss.muro.com.tr
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? PrimaryColor { get; set; }         // "#1B3B6F"
    public string? AccentColor { get; set; }          // "#10B981"
    public string? Domain { get; set; }               // Legacy/custom domain (optional)
    public string? FooterText { get; set; }

    // — Infrastructure —
    public string? ConnectionString { get; set; }     // Tenant-specific DB connection
    public string MeetingProviderType { get; set; } = "bbb";  // "bbb" veya "jitsi"
    public string? BbbServerUrl { get; set; }         // BBB sunucu adresi
    public string? BbbSecret { get; set; }            // BBB shared secret
    public string? JitsiServerUrl { get; set; }       // Jitsi sunucu adresi (opsiyonel)
    public string? ServerGroup { get; set; }          // "srv-2", "srv-3"

    // — Feature Flags (JSON) —
    public string? Features { get; set; }             // {"smsNotification":true,"aiAssistant":false}

    // — Quotas (VEP tarafından yönetilir) —
    public int? MaxStudents { get; set; }              // null = sınırsız (Aktif öğrenciler için)
    public int? MaxDemoStudents { get; set; }          // null = sınırsız (Demo öğrenciler için)
    public int? MaxCourses { get; set; }               // null = sınırsız
    public int? MaxSessionsPerDay { get; set; }        // null = sınırsız
    public decimal? StorageLimitGb { get; set; }       // null = sınırsız
    public int? MaxBbbParticipants { get; set; }       // null = sınırsız

    // — Status —
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<TenantMembership> Memberships { get; set; } = new List<TenantMembership>();
    public ICollection<Course> Courses { get; set; } = new List<Course>();
    public ICollection<Group> Groups { get; set; } = new List<Group>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
