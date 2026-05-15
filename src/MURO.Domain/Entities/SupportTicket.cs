using MURO.Domain.Enums;

namespace MURO.Domain.Entities;

public class SupportTicket
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public string Priority { get; set; } = "normal"; // low, normal, high
    public string Category { get; set; } = "technical"; // technical, institutional
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}
