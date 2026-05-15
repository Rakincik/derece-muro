using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class TenantMembership
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public UserRole Role { get; set; } = UserRole.Student;
    public string Status { get; set; } = "active"; // active, suspended
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
