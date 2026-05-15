using MURO.Application.DTOs.Admin;

namespace MURO.Application.Interfaces;

public interface IAdminTenantHealthService
{
    Task<(int, object?)> GetTenantHealthScore(Guid id);
    Task<(int, object?)> UpdateTenantMaintenance(Guid id, MaintenanceRequest request);
    Task<(int, object?)> GetTenantsMaintenanceStatus();
}
