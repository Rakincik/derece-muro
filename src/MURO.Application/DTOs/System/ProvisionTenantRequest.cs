namespace MURO.Application.DTOs.System;

public record ProvisionTenantRequest(
    Guid TenantId,
    string Name,
    string Slug,
    string? Subdomain,
    string? Domain,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? AccentColor,
    string? FooterText,
    string? Features,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string AdminPassword
);
