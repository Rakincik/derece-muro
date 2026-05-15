using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Type { get; set; } // "info", "warning", "exam", "assignment"
    public bool IsRead { get; set; } = false;
    public NotificationChannel Channel { get; set; } = NotificationChannel.System;
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
