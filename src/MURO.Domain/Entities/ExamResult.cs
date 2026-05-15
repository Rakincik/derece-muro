namespace MURO.Domain.Entities;

public class ExamResult
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid UserId { get; set; }
    public string Answers { get; set; } = string.Empty; // JSON: {"1":"A","2":"C",...}
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public int EmptyCount { get; set; }
    public double Score { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Exam Exam { get; set; } = null!;
    public User User { get; set; } = null!;
}
