using Microsoft.EntityFrameworkCore;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Grup ve Paket bazlı erişim kontrolü.
///
/// Erişim iki kaynaktan hesaplanır (EF Core uyumlu, tüm ID'ler bellekte):
///   1. UserPackage (aktif, süresi dolmamış) → PackageGroup → CourseGroup
///   2. GroupMember (direkt admin ataması) → CourseGroup
/// </summary>
public class GroupAccessService : IGroupAccessService
{
    private readonly MuroDbContext _context;

    public GroupAccessService(MuroDbContext context) => _context = context;

    // ── Yardımcı: Aktif paketlerin gruplarını çeker (filtre opsiyonel) ────────
    private async Task<List<Guid>> GetPkgGroupIdsAsync(Guid userId, CourseMode? modeFilter = null)
    {
        var now = DateTime.UtcNow;
        var packageIds = await _context.UserPackages
            .Where(up => up.UserId == userId
                      && up.Status == "active"
                      && (up.ExpiresAt == null || up.ExpiresAt > now))
            .Select(up => up.PackageId)
            .ToListAsync();

        if (packageIds.Count == 0) return new List<Guid>();

        var pgQuery = _context.PackageGroups.Where(pg => packageIds.Contains(pg.PackageId));

        if (modeFilter == CourseMode.Offline)
            pgQuery = pgQuery.Where(pg => pg.ContentMode == CourseMode.Offline || pg.ContentMode == CourseMode.Both);
        else if (modeFilter == CourseMode.Online)
            pgQuery = pgQuery.Where(pg => pg.ContentMode == CourseMode.Online || pg.ContentMode == CourseMode.Both);

        return await pgQuery.Select(pg => pg.GroupId).Distinct().ToListAsync();
    }

    // ── Yardımcı: Direkt GroupMember grubu (admin ataması) ───────────────────
    private async Task<List<Guid>> GetDirectGroupIdsAsync(Guid userId)
        => await _context.GroupMembers
            .Where(gm => gm.UserId == userId && gm.Status == "active")
            .Select(gm => gm.GroupId)
            .Distinct()
            .ToListAsync();

    // ── Gruplara ait kurs ID'lerini döner (tenant filtresi ile) ──────────────
    private async Task<HashSet<Guid>> CourseIdsForGroups(Guid tenantId, List<Guid> groupIds, CourseMode? modeFilter = null)
    {
        if (groupIds.Count == 0) return new HashSet<Guid>();

        var tenantGroupIds = await _context.Groups
            .Where(g => groupIds.Contains(g.Id) && g.TenantId == tenantId)
            .Select(g => g.Id)
            .ToListAsync();

        if (tenantGroupIds.Count == 0) return new HashSet<Guid>();

        var cgQuery = _context.CourseGroups.Where(cg => tenantGroupIds.Contains(cg.GroupId));

        if (modeFilter == CourseMode.Offline)
            cgQuery = cgQuery.Where(cg => cg.Mode == CourseMode.Offline || cg.Mode == CourseMode.Both);
        else if (modeFilter == CourseMode.Online)
            cgQuery = cgQuery.Where(cg => cg.Mode == CourseMode.Online || cg.Mode == CourseMode.Both);

        return (await cgQuery.Select(cg => cg.CourseId).Distinct().ToListAsync()).ToHashSet();
    }

    // ── Tüm erişilebilir kurs ID'leri ────────────────────────────────────────
    public async Task<HashSet<Guid>> GetAccessibleCourseIdsAsync(Guid tenantId, Guid userId)
    {
        if (await IsStaffAsync(userId))
            return (await _context.Courses.Where(c => c.TenantId == tenantId)
                .Select(c => c.Id).ToListAsync()).ToHashSet();

        var pkgGroupIds  = await GetPkgGroupIdsAsync(userId);
        var dirGroupIds  = await GetDirectGroupIdsAsync(userId);
        var allGroupIds  = pkgGroupIds.Union(dirGroupIds).ToList();

        return await CourseIdsForGroups(tenantId, allGroupIds);
    }

    // ── Video erişimi (ContentMode = Offline veya Both) ──────────────────────
    public async Task<HashSet<Guid>> GetVideoAccessibleCourseIdsAsync(Guid tenantId, Guid userId)
    {
        if (await IsStaffAsync(userId))
            return await GetAccessibleCourseIdsAsync(tenantId, userId);

        var pkgGroupIds = await GetPkgGroupIdsAsync(userId, CourseMode.Offline);
        var dirGroupIds = await GetDirectGroupIdsAsync(userId);

        // Paket grupları + direkt grupların video-mode kursları (UNION)
        var viaPackage  = await CourseIdsForGroups(tenantId, pkgGroupIds);
        var viaDirect   = await CourseIdsForGroups(tenantId, dirGroupIds, CourseMode.Offline);

        return viaPackage.Union(viaDirect).ToHashSet();
    }

    // ── Canlı ders erişimi (ContentMode = Online veya Both) ──────────────────
    public async Task<HashSet<Guid>> GetLiveAccessibleCourseIdsAsync(Guid tenantId, Guid userId)
    {
        if (await IsStaffAsync(userId))
            return await GetAccessibleCourseIdsAsync(tenantId, userId);

        var pkgGroupIds = await GetPkgGroupIdsAsync(userId, CourseMode.Online);
        var dirGroupIds = await GetDirectGroupIdsAsync(userId);

        var viaPackage  = await CourseIdsForGroups(tenantId, pkgGroupIds);
        var viaDirect   = await CourseIdsForGroups(tenantId, dirGroupIds, CourseMode.Online);

        return viaPackage.Union(viaDirect).ToHashSet();
    }

    // ── Tekil kurs erişim kontrolü ────────────────────────────────────────────
    public async Task<bool> CanAccessCourseAsync(Guid tenantId, Guid userId, Guid courseId)
    {
        if (await IsStaffAsync(userId)) return true;
        var ids = await GetAccessibleCourseIdsAsync(tenantId, userId);
        return ids.Contains(courseId);
    }

    // ── Staff kontrolü (DB'den rol okuma) ────────────────────────────────────
    public async Task<bool> IsStaffAsync(Guid userId)
    {
        var role = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync();
        return role is UserRole.Admin or UserRole.Instructor;
    }
}
