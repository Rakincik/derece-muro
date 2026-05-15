namespace MURO.Domain.Entities;

public class ExamAssignment
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public string TargetType { get; set; } = string.Empty; // "User", "Group", "Course"
    public Guid TargetId { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Exam Exam { get; set; } = null!;
}
