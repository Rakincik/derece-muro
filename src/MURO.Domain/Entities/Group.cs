using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }          // hex: #6366f1
    public string? EducationType { get; set; }   // Canlı, Offline, Kamp, Sınav, Hibrit, Demo
    public Guid TenantId { get; set; }
    public Guid? ParentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Group? Parent { get; set; }
    public ICollection<Group> Children { get; set; } = new List<Group>();
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<CourseGroup> CourseGroups { get; set; } = new List<CourseGroup>();
    public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
    public ICollection<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();
}
