namespace MURO.Application.DTOs.Tenants;

/// <summary>
/// Lightweight tenant info carried in request scope — branding + feature flags.
/// </summary>
public record TenantInfo(
    Guid Id,
    string Name,
    string? Subdomain,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? AccentColor,
    string? FooterText,
    string? BbbServerUrl,
    string? BbbSecret,
    string? ConnectionString,
    bool IsActive,
    Dictionary<string, bool> Features
);

/// <summary>
/// Public branding info returned to frontend (no secrets).
/// </summary>
public record TenantBrandingDto(
    string Name,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? AccentColor,
    string? FooterText
);

/// <summary>
/// Full tenant info for super-admin listing.
/// </summary>
public record TenantAdminDto(
    Guid Id,
    string Name,
    string Code,
    string? Subdomain,
    string? LogoUrl,
    string? PrimaryColor,
    string? AccentColor,
    string? BbbServerUrl,
    string? ServerGroup,
    bool IsActive,
    DateTime CreatedAt,
    Dictionary<string, bool> Features
);

/// <summary>
/// Create/update tenant request.
/// </summary>
public record UpsertTenantRequest(
    string Name,
    string Code,
    string? Subdomain,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? AccentColor,
    string? FooterText,
    string? ConnectionString,
    string? BbbServerUrl,
    string? BbbSecret,
    string? ServerGroup,
    Dictionary<string, bool>? Features
);

/// <summary>
/// Update tenant settings request.
/// </summary>
public record UpdateTenantSettingsRequest(
    string? Name,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? AccentColor,
    string? FooterText
);
