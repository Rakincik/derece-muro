using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Application.DTOs.Admin;

namespace MURO.Infrastructure.Services;

public class AdminSecurityService : IAdminSecurityService
{
    private readonly MuroDbContext _db;
    private readonly IBbbService _bbb;
    private readonly ILogger<AdminSecurityService> _logger;
    private readonly IConfiguration _config;

    public AdminSecurityService(MuroDbContext db, IBbbService bbb, ILogger<AdminSecurityService> logger, IConfiguration config)
    {
        _db = db;
        _bbb = bbb;
        _logger = logger;
        _config = config;
    }

    private (int, object?) Ok(object? data = null) => (200, data);
    private (int, object?) NotFound(object? data = null) => (404, data);
    private (int, object?) BadRequest(object? data = null) => (400, data);
    private (int, object?) Conflict(object? data = null) => (409, data);

    public async Task<(int, object?)> GetSecurityEvents(
        string? eventType, Guid? tenantId,
        int page = 1, int pageSize = 50)
    {
        var query = _db.SecurityEvents
            .Include(e => e.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(e => e.EventType == eventType);
        if (tenantId.HasValue)
            query = query.Where(e => e.TenantId == tenantId);

        var total = await query.CountAsync();
        var events = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new
            {
                e.Id, e.EventType, e.IpAddress, e.UserAgent, e.Details, e.CreatedAt, e.TenantId,
                userId = e.UserId,
                userName = e.User != null ? e.User.FirstName + " " + e.User.LastName : null,
                userEmail = e.User != null ? e.User.Email : null,
            })
            .ToListAsync();

        // Tip bazlÄ± sayÄ±lar (son 24 saat)
        var since = DateTime.UtcNow.AddHours(-24);
        var typeCounts = await _db.SecurityEvents
            .Where(e => e.CreatedAt >= since)
            .GroupBy(e => e.EventType)
            .Select(g => new { type = g.Key, count = g.Count() })
            .ToListAsync();

        return (200, new { items = events, totalCount = total, page, pageSize, last24h = typeCounts });
    }

    /// <summary>GÃ¼venlik Ã¶zeti â€” kilitli hesaplar, failed login sayÄ±larÄ±</summary>
    public async Task<(int, object?)> GetSecuritySummary()
    {
        var now = DateTime.UtcNow;
        var since24h = now.AddHours(-24);
        var since7d = now.AddDays(-7);

        var totalUsers = await _db.Users.CountAsync();
        var activeUsers = await _db.Users.CountAsync(u => u.IsActive);
        var lockedUsers = await _db.Users.CountAsync(u => u.LockoutUntil != null && u.LockoutUntil > now);
        var neverLoggedIn = await _db.Users.CountAsync(u => u.LastLoginAt == null);
        var recentLogins = await _db.Users.CountAsync(u => u.LastLoginAt >= since24h);

        var failedLogins24h = await _db.SecurityEvents.CountAsync(e => e.EventType == "LOGIN_FAILED" && e.CreatedAt >= since24h);
        var bruteForce24h = await _db.SecurityEvents.CountAsync(e => e.EventType == "BRUTE_FORCE_DETECTED" && e.CreatedAt >= since24h);
        var newIpLogins24h = await _db.SecurityEvents.CountAsync(e => e.EventType == "NEW_IP_LOGIN" && e.CreatedAt >= since24h);
        var sessionsKicked24h = await _db.SecurityEvents.CountAsync(e => e.EventType == "SESSION_KICKED" && e.CreatedAt >= since24h);
        var totalEvents7d = await _db.SecurityEvents.CountAsync(e => e.CreatedAt >= since7d);

        return Ok(new
        {
            users = new { totalUsers, activeUsers, lockedUsers, neverLoggedIn, recentLogins },
            events24h = new { failedLogins = failedLogins24h, bruteForce = bruteForce24h, newIpLogins = newIpLogins24h, sessionsKicked = sessionsKicked24h },
            totalEvents7d,
        });
    }

    /// <summary>Kilitli hesaplar listesi</summary>
    public async Task<(int, object?)> GetLockedAccounts()
    {
        var now = DateTime.UtcNow;
        var locked = await _db.Users
            .Where(u => u.LockoutUntil != null && u.LockoutUntil > now)
            .Include(u => u.TenantMemberships).ThenInclude(tm => tm.Tenant)
            .Select(u => new
            {
                u.Id, u.FirstName, u.LastName, u.Email,
                role = u.Role.ToString(),
                u.FailedLoginCount, u.LockoutUntil,
                tenants = u.TenantMemberships.Select(tm => new { tm.Tenant.Name, tm.Tenant.Code }),
            })
            .ToListAsync();
        return (200, new { items = locked, count = locked.Count });
    }

    /// <summary>Hesap kilidini kaldÄ±r</summary>
    public async Task<(int, object?)> UnlockAccount(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (404, null);
        user.LockoutUntil = null;
        user.FailedLoginCount = 0;
        await _db.SaveChangesAsync();
        return (200, new { success = true, message = $"{user.Email} kilidi kaldÄ±rÄ±ldÄ±" });
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // YEDEKLEME & KURTARMA
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private static string GetBackupDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "muro-backups");

    /// <summary>Backup politikasÄ± â€” aktif config</summary>
}
