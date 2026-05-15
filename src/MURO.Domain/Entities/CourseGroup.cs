using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class CourseGroup
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid GroupId { get; set; }
    public CourseMode Mode { get; set; } = CourseMode.Offline;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Course Course { get; set; } = null!;
    public Group Group { get; set; } = null!;
}
