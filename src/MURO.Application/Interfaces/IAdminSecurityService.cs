using MURO.Application.DTOs.Admin;

namespace MURO.Application.Interfaces;

public interface IAdminSecurityService
{
    Task<(int, object?)> GetSecurityEvents(string? eventType, Guid? tenantId, int page = 1, int pageSize = 50);
    Task<(int, object?)> GetSecuritySummary();
    Task<(int, object?)> GetLockedAccounts();
    Task<(int, object?)> UnlockAccount(Guid userId);
}
