using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MURO.Application.DTOs.Admin;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class AdminTenantQuotaService : IAdminTenantQuotaService
{
    private readonly MuroDbContext _db;
    private readonly ILogger<AdminTenantQuotaService> _logger;

    public AdminTenantQuotaService(MuroDbContext db, ILogger<AdminTenantQuotaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private (int, object?) Ok(object? data = null) => (200, data);

    public async Task<(int, object?)> UpdateTenantFeatures(Guid id, UpdateFeaturesRequest request)
    {
        var tenant = await _db.Tenants.FindAsync(id);
        if (tenant == null) return (404, new { error = "Tenant not found" });

        tenant.Features = request.Features;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Tenant {TenantId} ({Name}) features updated", id, tenant.Name);

        return (200, new { tenant.Id, tenant.Name, tenant.Features });
    }

    public async Task<(int, object?)> GetTenantQuotas(Guid id)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                limits = new
                {
                    maxStudents = t.MaxStudents,
                    maxDemoStudents = t.MaxDemoStudents,
                    maxCourses = t.MaxCourses,
                    maxSessionsPerDay = t.MaxSessionsPerDay,
                    storageLimitGb = t.StorageLimitGb,
                    maxBbbParticipants = t.MaxBbbParticipants,
                },
                usage = new
                {
                    currentStudents = t.Memberships.Count(m =>
                        m.Status == "active" && m.Role == UserRole.Student && m.User.StudentType != StudentType.Demo),
                    currentDemoStudents = t.Memberships.Count(m =>
                        m.Status == "active" && m.Role == UserRole.Student && m.User.StudentType == StudentType.Demo),
                    currentCourses = t.Courses.Count(),
                    todaySessions = t.Courses
                        .SelectMany(c => c.Sessions)
                        .Count(s => s.ScheduledStart >= DateTime.UtcNow.Date),
                    activeBbbParticipants = t.Courses
                        .SelectMany(c => c.Sessions)
                        .Where(s => s.Status == SessionStatus.Live)
                        .SelectMany(s => s.SessionAttendances)
                        .Count(a => a.LeftAt == null),
                },
            })
            .FirstOrDefaultAsync();

        if (tenant == null) return (404, new { error = "Tenant not found" });

        return (200, tenant);
    }

    public async Task<(int, object?)> UpdateTenantQuotas(Guid id, UpdateQuotasRequest request)
    {
        var tenant = await _db.Tenants.FindAsync(id);
        if (tenant == null) return (404, new { error = "Tenant not found" });

        tenant.MaxStudents = request.MaxStudents;
        tenant.MaxDemoStudents = request.MaxDemoStudents;
        tenant.MaxCourses = request.MaxCourses;
        tenant.MaxSessionsPerDay = request.MaxSessionsPerDay;
        tenant.StorageLimitGb = request.StorageLimitGb;
        tenant.MaxBbbParticipants = request.MaxBbbParticipants;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Tenant {TenantId} ({Name}) quotas updated", id, tenant.Name);

        return Ok(new
        {
            tenant.Id,
            tenant.Name,
            maxStudents = tenant.MaxStudents,
            maxDemoStudents = tenant.MaxDemoStudents,
            maxCourses = tenant.MaxCourses,
            maxSessionsPerDay = tenant.MaxSessionsPerDay,
            storageLimitGb = tenant.StorageLimitGb,
            maxBbbParticipants = tenant.MaxBbbParticipants,
        });
    }
}
