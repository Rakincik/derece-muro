using Microsoft.EntityFrameworkCore;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class PackageService : IPackageService
{
    private readonly MuroDbContext _context;
    private readonly INotificationService _notifications;
    private readonly ICacheService _cache;

    public PackageService(MuroDbContext context, INotificationService notifications, ICacheService cache)
    {
        _context = context;
        _notifications = notifications;
        _cache = cache;
    }

    // ── Paket CRUD ────────────────────────────────────────────────────────────

    public async Task<List<PackageDto>> GetPackagesAsync(Guid tenantId)
    {
        var cacheKey = $"{tenantId}:packages:list";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await _context.Packages
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId)
                .Include(p => p.PackageGroups).ThenInclude(pg => pg.Group)
                .Select(p => new PackageDto(
                    p.Id, p.TenantId, p.Name, p.Description, p.Price,
                    p.DurationDays, p.IsActive, p.CreatedAt,
                    p.PackageGroups.Select(pg => new PackageGroupDto(
                        pg.Id, pg.GroupId, pg.Group.Name, pg.ContentMode.ToString())).ToList(),
                    _context.UserPackages.Count(up => up.PackageId == p.Id && up.Status == "active")))
                .ToListAsync();
        }, TimeSpan.FromMinutes(5));
    }

    public async Task<PackageDto> GetPackageByIdAsync(Guid tenantId, Guid packageId)
    {
        var cacheKey = $"{tenantId}:packages:{packageId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var p = await _context.Packages
                .AsNoTracking()
                .Include(x => x.PackageGroups).ThenInclude(pg => pg.Group)
                .FirstOrDefaultAsync(x => x.Id == packageId && x.TenantId == tenantId)
                ?? throw new KeyNotFoundException("Paket bulunamadı.");

            return new PackageDto(
                p.Id, p.TenantId, p.Name, p.Description, p.Price,
                p.DurationDays, p.IsActive, p.CreatedAt,
                p.PackageGroups.Select(pg => new PackageGroupDto(
                    pg.Id, pg.GroupId, pg.Group.Name, pg.ContentMode.ToString())).ToList(),
                _context.UserPackages.Count(up => up.PackageId == p.Id && up.Status == "active"));
        }, TimeSpan.FromMinutes(5));
    }

    public async Task<PackageDto> CreatePackageAsync(Guid tenantId, CreatePackageRequest request)
    {
        var package = new Package
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            DurationDays = request.DurationDays,
            IsActive = true
        };
        _context.Packages.Add(package);

        foreach (var g in request.Groups)
        {
            _context.PackageGroups.Add(new PackageGroup
            {
                Id = Guid.NewGuid(),
                PackageId = package.Id,
                GroupId = g.GroupId,
                ContentMode = Enum.TryParse<Domain.Enums.CourseMode>(g.ContentMode, true, out var cm)
                    ? cm : Domain.Enums.CourseMode.Both
            });
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:packages:"); // Cache invalidation
        return await GetPackageByIdAsync(tenantId, package.Id);
    }

    public async Task<PackageDto> UpdatePackageAsync(Guid tenantId, Guid packageId, UpdatePackageRequest request)
    {
        var package = await _context.Packages
            .Include(p => p.PackageGroups)
            .FirstOrDefaultAsync(p => p.Id == packageId && p.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Paket bulunamadı.");

        if (request.Name != null) package.Name = request.Name;
        if (request.Description != null) package.Description = request.Description;
        if (request.Price.HasValue) package.Price = request.Price.Value;
        if (request.DurationDays.HasValue) package.DurationDays = request.DurationDays.Value;
        if (request.IsActive.HasValue) package.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:packages:"); // Cache invalidation
        return await GetPackageByIdAsync(tenantId, packageId);
    }

    public async Task DeletePackageAsync(Guid tenantId, Guid packageId)
    {
        var package = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == packageId && p.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Paket bulunamadı.");
        _context.Packages.Remove(package);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:packages:"); // Cache invalidation
    }

    // ── Kullanıcı Paket Aktivasyonu ───────────────────────────────────────────

    /// <summary>
    /// Bir kullanıcıya paket aktive eder.
    /// Süreyi Package.DurationDays'ten hesaplar; 0 = sınırsız (ExpiresAt=null).
    /// Admin override edebilmek için manualExpiresAt parametresi var.
    /// </summary>
    public async Task<UserPackageDto> ActivateUserPackageAsync(
        Guid userId, Guid packageId, string? orderId = null,
        string source = "admin_manual", DateTime? manualExpiresAt = null)
    {
        var package = await _context.Packages
            .Include(p => p.PackageGroups)
            .FirstOrDefaultAsync(p => p.Id == packageId)
            ?? throw new KeyNotFoundException("Paket bulunamadı.");

        var now = DateTime.UtcNow;
        DateTime? expiresAt = manualExpiresAt;
        if (!expiresAt.HasValue && package.DurationDays > 0)
            expiresAt = now.AddDays(package.DurationDays);

        var userPackage = new UserPackage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageId = packageId,
            OrderId = orderId,
            ActivatedAt = now,
            ExpiresAt = expiresAt,
            Status = "active",
            Source = source
        };
        _context.UserPackages.Add(userPackage);

        // Paket gruplarına otomatik GroupMember ekle (yoksa)
        foreach (var pg in package.PackageGroups)
        {
            var exists = await _context.GroupMembers
                .AnyAsync(gm => gm.UserId == userId && gm.GroupId == pg.GroupId);
            if (!exists)
            {
                _context.GroupMembers.Add(new GroupMember
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    GroupId = pg.GroupId,
                    Status = "active",
                    AddedAt = now
                });
            }
            else
            {
                // Daha önce kaldırılmışsa tekrar aktive et
                var gm = await _context.GroupMembers
                    .FirstAsync(gm => gm.UserId == userId && gm.GroupId == pg.GroupId);
                gm.Status = "active";
            }
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{package.TenantId}:packages:"); // Cache invalidation

        // Bildirim gönder
        await _notifications.CreateAsync(package.TenantId,
            new Application.DTOs.Notifications.CreateNotificationRequest(
                userId,
                "Paketiniz Aktive Edildi",
                $"\"{package.Name}\" paketiniz başarıyla aktive edildi." +
                (expiresAt.HasValue ? $" Erişim bitiş tarihiniz: {expiresAt.Value:dd.MM.yyyy}." : " Sınırsız erişim."),
                "PackageActivated",
                "InApp"));

        return new UserPackageDto(userPackage.Id, userId, packageId, package.Name,
            orderId, now, expiresAt, "active", source);
    }

    // ── Kullanıcı Paket Geçmişi ───────────────────────────────────────────────

    public async Task<List<UserPackageDto>> GetUserPackagesAsync(Guid userId)
    {
        return await _context.UserPackages
            .AsNoTracking()
            .Where(up => up.UserId == userId)
            .Include(up => up.Package)
            .OrderByDescending(up => up.ActivatedAt)
            .Select(up => new UserPackageDto(
                up.Id, up.UserId, up.PackageId, up.Package.Name,
                up.OrderId, up.ActivatedAt, up.ExpiresAt, up.Status, up.Source))
            .ToListAsync();
    }

    // ── Admin: Manuel Uzatma / İptal ──────────────────────────────────────────

    public async Task<UserPackageDto> ExtendPackageAsync(Guid userPackageId, int extraDays)
    {
        var up = await _context.UserPackages
            .Include(x => x.Package)
            .FirstOrDefaultAsync(x => x.Id == userPackageId)
            ?? throw new KeyNotFoundException("Kullanıcı paketi bulunamadı.");

        var now = DateTime.UtcNow;
        up.ExpiresAt = (up.ExpiresAt.HasValue && up.ExpiresAt > now)
            ? up.ExpiresAt.Value.AddDays(extraDays)
            : now.AddDays(extraDays);
        up.Status = "active";

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{up.Package.TenantId}:packages:"); // Cache invalidation

        return new UserPackageDto(up.Id, up.UserId, up.PackageId, up.Package.Name, // Kept original return type and logic
            up.OrderId, up.ActivatedAt, up.ExpiresAt, up.Status, up.Source);
    }

    public async Task CancelPackageAsync(Guid userPackageId)
    {
        var up = await _context.UserPackages
            .Include(x => x.Package).ThenInclude(p => p.PackageGroups)
            .FirstOrDefaultAsync(x => x.Id == userPackageId)
            ?? throw new KeyNotFoundException("Kullanıcı paketi bulunamadı.");

        up.Status = "cancelled";
        up.ExpiresAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{up.Package.TenantId}:packages:");

        await _notifications.CreateAsync(up.Package.TenantId,
            new Application.DTOs.Notifications.CreateNotificationRequest(
                up.UserId,
                "Paket İptal Edildi",
                $"\"{up.Package.Name}\" paketiniz iptal edildi. Erişiminiz sona erdi.",
                "PackageCancelled",
                "InApp"));
    }
}
