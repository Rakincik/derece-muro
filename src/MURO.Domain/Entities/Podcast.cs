using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class Podcast
{
    public Guid Id { get; set; }
    public Guid? CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TextContent { get; set; }       // Ham metin (admin girişi)
    public string? GeneratedScript { get; set; }   // Gemini'nin ürettiği temiz script
    public string? AudioFilePath { get; set; }
    public int? DurationSeconds { get; set; }
    public MediaStatus Status { get; set; } = MediaStatus.Processing;
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Course? Course { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
