using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Application.DTOs.Admin;

namespace MURO.Infrastructure.Services;

public class AdminUserService : IAdminUserService
{
    private readonly MuroDbContext _db;
    private readonly IBbbService _bbb;
    private readonly ILogger<AdminUserService> _logger;
    private readonly IConfiguration _config;

    public AdminUserService(MuroDbContext db, IBbbService bbb, ILogger<AdminUserService> logger, IConfiguration config)
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

    // E) KULLANICI YÃ–NETÄ°MÄ° â€” Platform Geneli
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>TÃ¼m kullanÄ±cÄ±larÄ± listele (cross-tenant, sayfalÄ±)</summary>
    public async Task<(int, object?)> GetUsers(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? role = null,
        Guid? tenantId = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 50);

        var query = _db.Users.AsNoTracking()
            .Include(u => u.TenantMemberships)
                .ThenInclude(tm => tm.Tenant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s) ||
                u.Email.ToLower().Contains(s));
        }

        if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, out var roleEnum))
            query = query.Where(u => u.Role == roleEnum);

        if (tenantId.HasValue)
            query = query.Where(u => u.TenantMemberships.Any(tm => tm.TenantId == tenantId.Value));

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone,
                Role = u.Role.ToString(),
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt,
                Tenants = u.TenantMemberships.Select(tm => new
                {
                    tm.TenantId,
                    TenantName = tm.Tenant.Name,
                    TenantSlug = tm.Tenant.Subdomain ?? tm.Tenant.Code,
                }).ToList(),
            })
            .ToListAsync();

        return Ok(new
        {
            items = users,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
        });
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    public async Task<(int, object?)> GetAllUsers(
        string? search, string? role, string? status,
        int page = 1, int pageSize = 50)
    {
        var query = _db.Users
            .Include(u => u.TenantMemberships).ThenInclude(tm => tm.Tenant)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.Email.Contains(search) || u.FirstName.Contains(search) || u.LastName.Contains(search));

        if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, true, out var roleEnum))
            query = query.Where(u => u.Role == roleEnum);

        if (status == "active") query = query.Where(u => u.IsActive);
        else if (status == "inactive") query = query.Where(u => !u.IsActive);
        else if (status == "locked") query = query.Where(u => u.LockoutUntil != null && u.LockoutUntil > DateTime.UtcNow);
        else if (status == "demo") query = query.Where(u => u.StudentType == StudentType.Demo);

        var total = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new
            {
                u.Id, u.FirstName, u.LastName, u.Email, u.Phone,
                role = u.Role.ToString(),
                studentType = u.StudentType.HasValue ? u.StudentType.ToString() : null,
                u.IsActive,
                u.CreatedAt, u.LastLoginAt,
                u.FailedLoginCount,
                isLocked = u.LockoutUntil != null && u.LockoutUntil > DateTime.UtcNow,
                lockoutUntil = u.LockoutUntil,
                tenants = u.TenantMemberships.Select(tm => new { tm.Tenant.Id, tm.Tenant.Name, tm.Tenant.Code }),
            })
            .ToListAsync();

        return (200, new { items = users, totalCount = total, page, pageSize });
    }

    /// <summary>Kurum bazlÄ± rol daÄŸÄ±lÄ±mÄ±</summary>
    public async Task<(int, object?)> GetRoleDistribution()
    {
        // Global rol sayÄ±larÄ±
        var globalRoles = await _db.Users
            .GroupBy(u => u.Role)
            .Select(g => new { role = g.Key.ToString(), count = g.Count() })
            .ToListAsync();

        // Tenant bazlÄ± rol sayÄ±larÄ±
        var tenantRoles = await _db.TenantMemberships
            .Include(tm => tm.User)
            .Include(tm => tm.Tenant)
            .GroupBy(tm => new { tm.TenantId, tm.Tenant.Name, tm.Tenant.Code })
            .Select(g => new
            {
                tenantId = g.Key.TenantId,
                tenantName = g.Key.Name,
                tenantCode = g.Key.Code,
                admin = g.Count(x => x.User.Role == UserRole.Admin),
                instructor = g.Count(x => x.User.Role == UserRole.Instructor),
                student = g.Count(x => x.User.Role == UserRole.Student),
                accountant = g.Count(x => x.User.Role == UserRole.Accountant),
                assistant = g.Count(x => x.User.Role == UserRole.Assistant),
                total = g.Count(),
            })
            .ToListAsync();

        var roles = new[] { "Admin", "Instructor", "Student", "Accountant", "Assistant" };
        return (200, new { roles, global = globalRoles, tenants = tenantRoles, totalUsers = globalRoles.Sum(r => r.count) });
    }

    /// <summary>Security event'leri (audit log)</summary>
}
