using MURO.Domain.Entities;

namespace MURO.Application.Interfaces;

public interface ITelegramBotService
{
    Task SendNewTicketNotificationAsync(SupportTicket ticket);
    Task SendReplyToTicketAsync(Guid ticketId, string replyText);
}
