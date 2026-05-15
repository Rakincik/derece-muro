using MURO.Application.DTOs;
using MURO.Application.DTOs.Support;

namespace MURO.Application.Interfaces;

public interface ISupportService
{
    // Tickets
    Task<PagedResult<TicketListDto>> GetTicketsAsync(Guid tenantId, int page, int pageSize, string? status);
    Task<TicketDetailDto> GetTicketByIdAsync(Guid tenantId, Guid ticketId);
    Task<TicketListDto> CreateTicketAsync(Guid tenantId, Guid userId, CreateTicketRequest request);
    Task<TicketMessageDto> ReplyAsync(Guid tenantId, Guid ticketId, Guid senderId, ReplyTicketRequest request);
    Task CloseTicketAsync(Guid tenantId, Guid ticketId);
    // FAQ
    Task<List<FaqDto>> GetFaqsAsync(Guid tenantId);
    Task<FaqDto> CreateFaqAsync(Guid tenantId, CreateFaqRequest request);
    Task<FaqDto> UpdateFaqAsync(Guid tenantId, Guid faqId, UpdateFaqRequest request);
    Task DeleteFaqAsync(Guid tenantId, Guid faqId);
}
