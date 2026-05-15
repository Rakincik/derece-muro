using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MURO.Application.DTOs.Admin;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class AdminTenantManagementService : IAdminTenantManagementService
{
    private readonly MuroDbContext _db;
    private readonly ILogger<AdminTenantManagementService> _logger;

    public AdminTenantManagementService(MuroDbContext db, ILogger<AdminTenantManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private (int, object?) Ok(object? data = null) => (200, data);

    public async Task<(int, object?)> GetTenants(int page = 1, int pageSize = 20, string? search = null, string? status = null)
    {
        var query = _db.Tenants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(s) ||
                (t.Code != null && t.Code.ToLower().Contains(s)) ||
                (t.Subdomain != null && t.Subdomain.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            query = status switch
            {
                "Active" => query.Where(t => t.IsActive),
                "Suspended" or "Disabled" => query.Where(t => !t.IsActive),
                _ => query
            };
        }

        var totalCount = await query.CountAsync();
        var tenants = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Code,
                Slug = t.Subdomain ?? t.Code,
                t.Subdomain,
                t.IsActive,
                Status = t.IsActive ? "Active" : "Suspended",
                t.LogoUrl,
                t.Domain,
                t.ServerGroup,
                t.CreatedAt,
                UserCount = t.Memberships.Count(m => m.Status == "active"),
                CourseCount = t.Courses.Count(),
                ActiveSessionCount = t.Courses
                    .SelectMany(c => c.Sessions)
                    .Count(s => s.Status == SessionStatus.Live),
            })
            .ToListAsync();

        return Ok(new
        {
            items = tenants,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
        });
    }

    public async Task<(int, object?)> GetTenantDetail(Guid id)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Code,
                Slug = t.Subdomain ?? t.Code,
                t.Subdomain,
                t.Domain,
                t.IsActive,
                Status = t.IsActive ? "Active" : "Suspended",
                t.LogoUrl,
                t.FaviconUrl,
                t.PrimaryColor,
                t.AccentColor,
                t.FooterText,
                t.ServerGroup,
                t.BbbServerUrl,
                t.Features,
                t.CreatedAt,
                TotalUsers = t.Memberships.Count(m => m.Status == "active"),
                StudentCount = t.Memberships.Count(m => m.Status == "active" && m.Role == UserRole.Student),
                TeacherCount = t.Memberships.Count(m => m.Status == "active" &&
                    (m.Role == UserRole.Instructor || m.Role == UserRole.Admin)),
                CourseCount = t.Courses.Count(),
                PublishedCourseCount = t.Courses.Count(c => c.IsPublished),
                ActiveSessionCount = t.Courses
                    .SelectMany(c => c.Sessions)
                    .Count(s => s.Status == SessionStatus.Live),
                TotalRecordings = t.Courses
                    .SelectMany(c => c.Sessions)
                    .Count(s => s.Recording != null),
            })
            .FirstOrDefaultAsync();

        if (tenant == null) return (404, new { error = "Tenant not found" });

        return (200, tenant);
    }

    public async Task<(int, object?)> UpdateTenantStatus(Guid id, TenantStatusRequest request)
    {
        var tenant = await _db.Tenants.FindAsync(id);
        if (tenant == null) return (404, new { error = "Tenant not found" });

        tenant.IsActive = request.IsActive;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Tenant {TenantId} ({Name}) status updated to {IsActive}",
            id, tenant.Name, request.IsActive);

        return Ok(new
        {
            tenant.Id,
            tenant.Name,
            tenant.IsActive,
            Status = tenant.IsActive ? "Active" : "Suspended",
        });
    }

    public async Task<(int, object?)> CreateTenant(CreateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return (400, new { error = "Kurum adı zorunludur" });
        if (string.IsNullOrWhiteSpace(request.Code))
            return (400, new { error = "Kurum kodu zorunludur" });
        if (string.IsNullOrWhiteSpace(request.AdminEmail))
            return (400, new { error = "Admin e-posta adresi zorunludur" });
        if (string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Length < 6)
            return (400, new { error = "Admin şifresi en az 6 karakter olmalıdır" });

        var codeExists = await _db.Tenants.AnyAsync(t => t.Code == request.Code);
        if (codeExists)
            return (409, new { error = $"'{request.Code}' kodu zaten kullanılıyor" });

        var emailExists = await _db.Users.AnyAsync(u => u.Email == request.AdminEmail.ToLower());
        if (emailExists)
            return (409, new { error = $"'{request.AdminEmail}' e-posta adresi zaten kayıtlı" });

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var tenant = new MURO.Domain.Entities.Tenant
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Code = request.Code,
                Subdomain = request.Subdomain ?? request.Code,
                Domain = request.Domain,
                LogoUrl = request.LogoUrl,
                FaviconUrl = request.FaviconUrl,
                PrimaryColor = request.PrimaryColor,
                AccentColor = request.AccentColor,
                FooterText = request.FooterText,
                BbbServerUrl = request.BbbServerUrl,
                BbbSecret = request.BbbSecret,
                ServerGroup = request.ServerGroup,
                Features = request.Features,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Tenants.Add(tenant);

            var adminUser = new MURO.Domain.Entities.User
            {
                Id = Guid.NewGuid(),
                FirstName = request.AdminFirstName ?? "Admin",
                LastName = request.AdminLastName ?? tenant.Name,
                Email = request.AdminEmail.ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Users.Add(adminUser);

            var membership = new MURO.Domain.Entities.TenantMembership
            {
                Id = Guid.NewGuid(),
                UserId = adminUser.Id,
                TenantId = tenant.Id,
                Role = UserRole.Admin,
                Status = "active",
                JoinedAt = DateTime.UtcNow,
            };
            _db.TenantMemberships.Add(membership);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "New tenant created: {TenantId} ({Name}) with admin {AdminEmail}",
                tenant.Id, tenant.Name, adminUser.Email);

            return Ok(new
            {
                tenant.Id,
                tenant.Name,
                tenant.Code,
                Slug = tenant.Subdomain ?? tenant.Code,
                tenant.Subdomain,
                tenant.IsActive,
                Status = "Active",
                AdminUser = new { adminUser.Id, adminUser.Email, adminUser.FirstName, adminUser.LastName },
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "CreateTenant failed for {Name}", request.Name);
            return (500, new { error = $"Kurum oluşturulurken hata: {ex.Message}" });
        }
    }
}
