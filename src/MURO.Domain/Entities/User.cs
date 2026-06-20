using MURO.Domain.Common;
using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class User : ISoftDeletable
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? TcNo { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public StudentType? StudentType { get; set; }
    public DateTime? DemoExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    // Brute-force koruması
    public int FailedLoginCount { get; set; } = 0;
    public DateTime? LockoutUntil { get; set; }

    // Navigation

    public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
    public ICollection<DeviceSession> DeviceSessions { get; set; } = new List<DeviceSession>();
    public ICollection<VideoProgress> VideoProgresses { get; set; } = new List<VideoProgress>();
    public ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
