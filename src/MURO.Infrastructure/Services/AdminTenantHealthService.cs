using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MURO.Application.DTOs.Admin;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class AdminTenantHealthService : IAdminTenantHealthService
{
    private readonly MuroDbContext _db;
    private readonly ILogger<AdminTenantHealthService> _logger;

    public AdminTenantHealthService(MuroDbContext db, ILogger<AdminTenantHealthService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private (int, object?) Ok(object? data = null) => (200, data);

    public async Task<(int, object?)> GetTenantHealthScore(Guid id)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.BbbServerUrl,
                TotalUsers = t.Memberships.Count(m => m.Status == "active"),
                ActiveUsersLast7Days = t.Memberships.Count(m =>
                    m.Status == "active" && m.User.LastLoginAt != null &&
                    m.User.LastLoginAt >= DateTime.UtcNow.AddDays(-7)),
                SuspendedUsers = t.Memberships.Count(m => m.Status == "suspended"),
                TotalCourses = t.Courses.Count(),
                PublishedCourses = t.Courses.Count(c => c.IsPublished),
                ActiveSessions = t.Courses
                    .SelectMany(c => c.Sessions)
                    .Count(s => s.Status == SessionStatus.Live),
                TotalRecordings = t.Courses
                    .SelectMany(c => c.Sessions)
                    .Count(s => s.Recording != null),
                FailedRecordings = t.Courses
                    .SelectMany(c => c.Sessions)
                    .Count(s => s.Recording != null && s.Recording.Status == MediaStatus.Failed),
            })
            .FirstOrDefaultAsync();

        if (tenant == null) return (404, new { error = "Tenant not found" });

        var userActivityRate = tenant.TotalUsers > 0
            ? (double)tenant.ActiveUsersLast7Days / tenant.TotalUsers * 100 : 0;
        var publishRate = tenant.TotalCourses > 0
            ? (double)tenant.PublishedCourses / tenant.TotalCourses * 100 : 0;
        var suspendedRate = tenant.TotalUsers > 0
            ? (double)tenant.SuspendedUsers / tenant.TotalUsers * 100 : 0;

        var score = (int)Math.Round(
            userActivityRate * 0.4 +
            publishRate * 0.3 +
            (100 - suspendedRate) * 0.2 +
            (tenant.ActiveSessions > 0 ? 10 : 0)
        );
        score = Math.Clamp(score, 0, 100);

        var status = score >= 75 ? "healthy" : score >= 40 ? "warning" : "critical";

        return Ok(new
        {
            tenant.Id,
            tenant.Name,
            score,
            status,
            signals = new
            {
                userActivityRate = Math.Round(userActivityRate, 1),
                publishRate = Math.Round(publishRate, 1),
                suspendedRate = Math.Round(suspendedRate, 1),
                hasActiveSessions = tenant.ActiveSessions > 0,
                activeSessionCount = tenant.ActiveSessions,
                hasBbbConfigured = !string.IsNullOrEmpty(tenant.BbbServerUrl),
                failedRecordings = tenant.FailedRecordings,
            },
        });
    }

    public async Task<(int, object?)> UpdateTenantMaintenance(Guid id, MaintenanceRequest request)
    {
        var tenant = await _db.Tenants.FindAsync(id);
        if (tenant == null) return (404, new { error = "Tenant not found" });

        if (request.MaintenanceMode)
        {
            tenant.IsActive = false;
            var features = new Dictionary<string, object>();
            try { features = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(tenant.Features ?? "{}") ?? new(); } catch { }
            features["_maintenance"] = true;
            features["_maintenanceMessage"] = request.Message ?? "Bakım çalışması yapılmaktadır.";
            features["_maintenanceStarted"] = DateTime.UtcNow.ToString("O");
            tenant.Features = System.Text.Json.JsonSerializer.Serialize(features);
        }
        else
        {
            tenant.IsActive = true;
            var features = new Dictionary<string, object>();
            try { features = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(tenant.Features ?? "{}") ?? new(); } catch { }
            features.Remove("_maintenance");
            features.Remove("_maintenanceMessage");
            features.Remove("_maintenanceStarted");
            tenant.Features = System.Text.Json.JsonSerializer.Serialize(features);
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Tenant {TenantId} ({Name}) maintenance mode: {Mode}",
            id, tenant.Name, request.MaintenanceMode);

        return Ok(new
        {
            tenant.Id,
            tenant.Name,
            maintenanceMode = request.MaintenanceMode,
            tenant.IsActive,
        });
    }

    public async Task<(int, object?)> GetTenantsMaintenanceStatus()
    {
        var tenants = await _db.Tenants
            .AsNoTracking()
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Code,
                t.IsActive,
                t.Features,
            })
            .ToListAsync();

        var result = tenants.Select(t =>
        {
            var features = new Dictionary<string, object>();
            try { features = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(t.Features ?? "{}") ?? new(); } catch { }

            return new
            {
                t.Id,
                t.Name,
                t.Code,
                t.IsActive,
                maintenanceMode = features.ContainsKey("_maintenance"),
                maintenanceMessage = features.TryGetValue("_maintenanceMessage", out var msg) ? msg?.ToString() : null,
                maintenanceStarted = features.TryGetValue("_maintenanceStarted", out var started) ? started?.ToString() : null,
            };
        }).ToList();

        return Ok(new { items = result });
    }
}
