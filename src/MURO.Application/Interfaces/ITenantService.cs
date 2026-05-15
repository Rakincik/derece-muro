using MURO.Application.DTOs.Tenants;

namespace MURO.Application.Interfaces;

/// <summary>
/// Manages the current tenant context for multi-tenant isolation.
/// </summary>
public interface ITenantService
{
    Guid? CurrentTenantId { get; }
    TenantInfo? CurrentTenant { get; }

    void SetCurrentTenant(Guid tenantId);
    void SetCurrentTenant(TenantInfo tenant);

    /// <summary>Returns the connection string for the active tenant's DB (null = use default).</summary>
    string? GetConnectionString();

    /// <summary>Check if a feature flag is enabled for the current tenant.</summary>
    bool HasFeature(string featureName);

    Task<TenantAdminDto?> GetSettingsAsync();
    Task<TenantAdminDto?> UpdateSettingsAsync(string? name, string? logoUrl, string? faviconUrl, string? primaryColor, string? accentColor, string? footerText);
}
