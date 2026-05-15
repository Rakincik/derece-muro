namespace MURO.Application.DTOs.Admin;

public record TenantStatusRequest(bool IsActive);

public record CreateTenantRequest(
    // Zorunlu
    string Name,
    string Code,
    string AdminEmail,
    string AdminPassword,
    // Opsiyonel - Temel
    string? Subdomain = null,
    string? Domain = null,
    string? AdminFirstName = null,
    string? AdminLastName = null,
    // Opsiyonel - Branding
    string? LogoUrl = null,
    string? FaviconUrl = null,
    string? PrimaryColor = null,
    string? AccentColor = null,
    string? FooterText = null,
    // Opsiyonel - Altyapı
    string? BbbServerUrl = null,
    string? BbbSecret = null,
    string? ServerGroup = null,
    // Opsiyonel - Özellikler
    string? Features = null
);

public record UpdateFeaturesRequest(string? Features);

public record UpdateQuotasRequest(
    int? MaxStudents,
    int? MaxDemoStudents,
    int? MaxCourses,
    int? MaxSessionsPerDay,
    decimal? StorageLimitGb,
    int? MaxBbbParticipants
);

public record RetryRequest(string Type);

public record MaintenanceRequest(bool MaintenanceMode, string? Message = null);

public record DbTestRequest(string ConnectionString);
