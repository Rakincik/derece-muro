using Microsoft.EntityFrameworkCore;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class PlatformAnalyticsService : IPlatformAnalyticsService
{
    private readonly MuroDbContext _db;

    public PlatformAnalyticsService(MuroDbContext db)
    {
        _db = db;
    }

    public async Task<PlatformStatsReport> GetPlatformStatsAsync()
    {
        var report = new PlatformStatsReport { Timestamp = DateTime.UtcNow };

        report.TotalUsers = await _db.Users.CountAsync();

        report.TotalStudents = await _db.TenantMemberships
            .Where(m => m.Status == "active" && m.Role == Domain.Enums.UserRole.Student)
            .CountAsync();

        report.TotalTeachers = await _db.TenantMemberships
            .Where(m => m.Status == "active" &&
                   (m.Role == Domain.Enums.UserRole.Instructor || m.Role == Domain.Enums.UserRole.Admin))
            .CountAsync();

        report.ActiveUsersToday = await _db.Users
            .Where(u => u.LastLoginAt != null && u.LastLoginAt.Value.Date == DateTime.UtcNow.Date)
            .CountAsync();

        var lastLogin = await _db.Users
            .Where(u => u.LastLoginAt != null)
            .OrderByDescending(u => u.LastLoginAt)
            .Select(u => u.LastLoginAt)
            .FirstOrDefaultAsync();
        report.LastLoginAt = lastLogin?.ToString("o");

        report.TotalCourses = await _db.Courses.CountAsync();
        report.PublishedCourses = await _db.Courses.Where(c => c.IsPublished).CountAsync();
        report.TotalSessions = await _db.Sessions.CountAsync();
        report.ActiveSessions = await _db.Sessions
            .Where(s => s.Status == Domain.Enums.SessionStatus.Live)
            .CountAsync();

        report.TotalRecordings = await _db.SessionRecordings.CountAsync();
        report.PendingRecordings = await _db.SessionRecordings
            .Where(r => r.Status == Domain.Enums.MediaStatus.Uploading)
            .CountAsync();
        report.ProcessingRecordings = await _db.SessionRecordings
            .Where(r => r.Status == Domain.Enums.MediaStatus.Processing)
            .CountAsync();
        report.CompletedRecordingsToday = await _db.SessionRecordings
            .Where(r => r.Status == Domain.Enums.MediaStatus.Ready
                     && r.CreatedAt.Date == DateTime.UtcNow.Date)
            .CountAsync();

        report.TotalMediaAssets = await _db.MediaAssets.CountAsync();

        report.TotalTenants = await _db.Tenants.CountAsync();
        report.ActiveTenants = await _db.Tenants.Where(t => t.IsActive).CountAsync();

        report.TotalPackages = await _db.Packages.CountAsync();
        report.ActiveUserPackages = await _db.UserPackages
            .Where(up => up.ExpiresAt == null || up.ExpiresAt > DateTime.UtcNow)
            .CountAsync();

        return report;
    }
}
