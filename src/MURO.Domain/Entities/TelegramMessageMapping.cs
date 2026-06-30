using System;

namespace MURO.Domain.Entities;

public class TelegramMessageMapping
{
    public Guid Id { get; set; }
    public long TelegramMessageId { get; set; }
    public long TelegramChatId { get; set; }
    public Guid SupportTicketId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SupportTicket SupportTicket { get; set; } = null!;
}
