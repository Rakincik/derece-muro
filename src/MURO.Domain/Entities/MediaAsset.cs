using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class MediaAsset
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? HlsPath { get; set; }
    public string? ThumbnailPath { get; set; }
    public int? DurationSeconds { get; set; }
    public MediaStatus Status { get; set; } = MediaStatus.Uploading;
    public Guid? CourseId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? FolderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Course? Course { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public MediaFolder? Folder { get; set; }
    public ICollection<CourseMedia> CourseMedias { get; set; } = new List<CourseMedia>();
    public ICollection<VideoProgress> VideoProgresses { get; set; } = new List<VideoProgress>();
}
