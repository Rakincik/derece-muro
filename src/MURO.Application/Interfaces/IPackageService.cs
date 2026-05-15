namespace MURO.Application.Interfaces;

// DTO'lar
public record PackageGroupDto(Guid Id, Guid GroupId, string GroupName, string ContentMode);
public record PackageDto(
    Guid Id, Guid TenantId, string Name, string? Description,
    decimal Price, int DurationDays, bool IsActive, DateTime CreatedAt,
    List<PackageGroupDto> Groups, int ActiveUserCount);
public record UserPackageDto(
    Guid Id, Guid UserId, Guid PackageId, string PackageName,
    string? OrderId, DateTime ActivatedAt, DateTime? ExpiresAt,
    string Status, string Source);

// İstek DTO'ları
public record PackageGroupRequest(Guid GroupId, string ContentMode);
public record CreatePackageRequest(
    string Name, string? Description, decimal Price,
    int DurationDays, List<PackageGroupRequest> Groups);
public record UpdatePackageRequest(
    string? Name, string? Description,
    decimal? Price, int? DurationDays, bool? IsActive);

/// <summary>
/// Paket yönetimi ve kullanıcı etkileşimleri
/// </summary>
public interface IPackageService
{
    Task<List<PackageDto>> GetPackagesAsync(Guid tenantId);
    Task<PackageDto> GetPackageByIdAsync(Guid tenantId, Guid packageId);
    Task<PackageDto> CreatePackageAsync(Guid tenantId, CreatePackageRequest request);
    Task<PackageDto> UpdatePackageAsync(Guid tenantId, Guid packageId, UpdatePackageRequest request);
    Task DeletePackageAsync(Guid tenantId, Guid packageId);

    /// <summary>Paketi kullanıcıya aktive et (webhook veya admin)</summary>
    Task<UserPackageDto> ActivateUserPackageAsync(
        Guid userId, Guid packageId, string? orderId = null,
        string source = "admin_manual", DateTime? manualExpiresAt = null);

    Task<List<UserPackageDto>> GetUserPackagesAsync(Guid userId);

    /// <summary>Admin: paketi belirli gün uzat</summary>
    Task<UserPackageDto> ExtendPackageAsync(Guid userPackageId, int extraDays);

    /// <summary>Admin: paketi iptal et</summary>
    Task CancelPackageAsync(Guid userPackageId);
}
