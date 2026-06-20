using MURO.Application.DTOs;
using MURO.Application.DTOs.Support;

namespace MURO.Application.Interfaces;

public interface ISupportService
{
    // Tickets
    Task<PagedResult<TicketListDto>> GetTicketsAsync(int page, int pageSize, string? status, Guid? userId = null);
    Task<TicketDetailDto> GetTicketByIdAsync(Guid ticketId);
    Task<TicketListDto> CreateTicketAsync(Guid userId, CreateTicketRequest request);
    Task<TicketMessageDto> ReplyAsync(Guid ticketId, Guid senderId, ReplyTicketRequest request);
    Task CloseTicketAsync(Guid ticketId);
    // FAQ
    Task<List<FaqDto>> GetFaqsAsync();
    Task<FaqDto> CreateFaqAsync(CreateFaqRequest request);
    Task<FaqDto> UpdateFaqAsync(Guid faqId, UpdateFaqRequest request);
    Task DeleteFaqAsync(Guid faqId);
}
