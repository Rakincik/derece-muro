using MURO.Application.DTOs.Security;

namespace MURO.Application.Interfaces;

public interface ISecurityService
{
    Task<SecurityEventPageDto> GetEventsAsync(Guid tenantId, DateTime? from, DateTime? to, Guid? userId, string? eventType, int page, int pageSize);
    Task<List<SecuritySummaryDto>> GetSummaryAsync(Guid tenantId);
    Task<List<SecurityEventDto>> GetSuspiciousActivityAsync(Guid tenantId);
}
