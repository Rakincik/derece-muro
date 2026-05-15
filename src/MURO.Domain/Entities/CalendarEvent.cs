namespace MURO.Domain.Entities;

public class CalendarEvent
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = "Ders"; // Ders, Ödev, Sınav, Etkinlik
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Color { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Group? Group { get; set; }
    public Course? Course { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
