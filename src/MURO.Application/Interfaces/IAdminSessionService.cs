using MURO.Application.DTOs.Admin;

namespace MURO.Application.Interfaces;

public interface IAdminSessionService
{
    Task<(int, object?)> GetActiveSessions();
    Task<(int, object?)> GetTodaySessions();
    Task<(int, object?)> GetSessionDetail(Guid id);
    Task<(int, object?)> ForceEndSession(Guid id);
    Task<(int, object?)> GetRecordings(int page = 1, int pageSize = 20, string? status = null, Guid? tenantId = null);
}
