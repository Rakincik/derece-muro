using MURO.Application.DTOs.Admin;

namespace MURO.Application.Interfaces;

public interface IAdminUserService
{
    Task<(int, object?)> GetUsers(int page = 1, int pageSize = 20, string? search = null, string? role = null, Guid? tenantId = null);
    Task<(int, object?)> GetAllUsers(string? search, string? role, string? status, int page = 1, int pageSize = 50);
    Task<(int, object?)> GetRoleDistribution();
}
