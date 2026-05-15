using MURO.Application.DTOs.Admin;

namespace MURO.Application.Interfaces;

public interface IAdminTenantQuotaService
{
    Task<(int, object?)> UpdateTenantFeatures(Guid id, UpdateFeaturesRequest request);
    Task<(int, object?)> GetTenantQuotas(Guid id);
    Task<(int, object?)> UpdateTenantQuotas(Guid id, UpdateQuotasRequest request);
}
