using MURO.Application.DTOs.Admin;

namespace MURO.Application.Interfaces;

public interface IAdminTenantManagementService
{
    Task<(int, object?)> GetTenants(int page = 1, int pageSize = 20, string? search = null, string? status = null);
    Task<(int, object?)> GetTenantDetail(Guid id);
    Task<(int, object?)> UpdateTenantStatus(Guid id, TenantStatusRequest request);
    Task<(int, object?)> CreateTenant(CreateTenantRequest request);
}
