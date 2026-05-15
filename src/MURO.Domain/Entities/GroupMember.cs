using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class GroupMember
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
    public UserRole Role { get; set; } = UserRole.Student;
    public string Status { get; set; } = "active"; // active, removed
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Group Group { get; set; } = null!;
}
