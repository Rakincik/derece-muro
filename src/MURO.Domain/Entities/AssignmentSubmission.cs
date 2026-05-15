namespace MURO.Domain.Entities;

public class AssignmentSubmission
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid UserId { get; set; }
    public string? FileUrl { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public int? Score { get; set; }
    public string? Feedback { get; set; }

    // Navigation
    public Assignment Assignment { get; set; } = null!;
    public User User { get; set; } = null!;
}
