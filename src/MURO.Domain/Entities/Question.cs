namespace MURO.Domain.Entities;

public class Question
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid InstructorId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? AudioUrl { get; set; }
    public string? Note { get; set; } // Private student-facing note
    public string? Answer { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public string Status { get; set; } = "pending"; // pending, answered
    public Guid? CourseId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public User Instructor { get; set; } = null!;
    public Course? Course { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
