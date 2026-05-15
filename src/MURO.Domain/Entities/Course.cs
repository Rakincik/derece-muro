using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class Course
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public CourseType CourseType { get; set; } = CourseType.Online;
    public CourseMode Mode { get; set; } = CourseMode.Offline;
    public Guid? InstructorId { get; set; }
    public Guid TenantId { get; set; }
    public bool IsPublished { get; set; } = false;
    public int Order { get; set; } = 0;
    public DateTime? StartDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? Instructor { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public ICollection<CourseGroup> CourseGroups { get; set; } = new List<CourseGroup>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<MediaAsset> MediaAssets { get; set; } = new List<MediaAsset>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<Podcast> Podcasts { get; set; } = new List<Podcast>();
    public ICollection<CourseMaterial> Materials { get; set; } = new List<CourseMaterial>();
    public ICollection<CourseMedia> CourseMedias { get; set; } = new List<CourseMedia>();
}
