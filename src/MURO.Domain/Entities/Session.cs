using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class Session
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; } = 0;
    public string? VideoUrl { get; set; }
    public int? DurationMinutes { get; set; }
    public bool IsFree { get; set; } = false;
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public string? BbbMeetingId { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
    public bool RecordingEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Course Course { get; set; } = null!;
    public SessionRecording? Recording { get; set; }
    public ICollection<SessionAttendance> SessionAttendances { get; set; } = new List<SessionAttendance>();
}
