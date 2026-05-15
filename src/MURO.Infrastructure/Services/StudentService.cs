using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Calendar;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class StudentService : IStudentService
{
    private readonly MuroDbContext _context;
    private readonly IGroupAccessService _groupAccess;
    private readonly ICacheService _cache;

    public StudentService(MuroDbContext context, IGroupAccessService groupAccess, ICacheService cache)
    {
        _context = context;
        _groupAccess = groupAccess;
        _cache = cache;
    }

    public async Task<List<CalendarEventDto>> GetCalendarEventsAsync(Guid tenantId, Guid userId, DateTime from, DateTime to)
    {
        var cacheKey = $"{tenantId}:student:calendar:{userId}:{from:yyyyMMdd}:{to:yyyyMMdd}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var accessibleCourseIds = await _groupAccess.GetAccessibleCourseIdsAsync(tenantId, userId);

            // 1) Scheduled sessions from the student's courses
            var sessionEvents = await _context.Sessions
                .AsNoTracking()
                .Include(s => s.Course)
                .Where(s => s.Course.TenantId == tenantId 
                         && s.Course.IsPublished 
                         && accessibleCourseIds.Contains(s.CourseId)
                         && s.ScheduledStart >= from 
                         && s.ScheduledStart <= to)
                .OrderBy(s => s.ScheduledStart)
                .Select(s => new CalendarEventDto(
                    s.Id, s.Title, s.Description,
                    "session",
                    s.ScheduledStart ?? DateTime.UtcNow,
                    s.ScheduledEnd ?? (s.ScheduledStart ?? DateTime.UtcNow).AddMinutes(s.DurationMinutes ?? 60),
                    null, null,
                    s.CourseId, s.Course != null ? s.Course.Title : null,
                    "#4ade80", s.CreatedAt))
                .ToListAsync();

            // 2) Calendar events from the student's groups + tenant-wide events (GroupId == null)
            var studentGroupIds = await _context.GroupMembers
                .AsNoTracking()
                .Where(gm => gm.UserId == userId && gm.Status == "active")
                .Join(_context.Groups.Where(g => g.TenantId == tenantId),
                      gm => gm.GroupId, g => g.Id, (gm, g) => g.Id)
                .ToListAsync();

            var calendarEvents = await _context.CalendarEvents
                .AsNoTracking()
                .Include(e => e.Group)
                .Include(e => e.Course)
                .Where(e => e.TenantId == tenantId
                         && e.StartDate >= from && e.EndDate <= to
                         && ((e.GroupId.HasValue && studentGroupIds.Contains(e.GroupId.Value)) 
                             || (e.CourseId.HasValue && accessibleCourseIds.Contains(e.CourseId.Value))))
                .OrderBy(e => e.StartDate)
                .Select(e => new CalendarEventDto(
                    e.Id, e.Title, e.Description, e.EventType,
                    e.StartDate, e.EndDate,
                    e.GroupId, e.Group != null ? e.Group.Name : null,
                    e.CourseId, e.Course != null ? e.Course.Title : null,
                    e.Color, e.CreatedAt))
                .ToListAsync();

            // Merge & sort
            return sessionEvents.Concat(calendarEvents)
                .OrderBy(e => e.StartDate)
                .ToList();
        }, TimeSpan.FromMinutes(2));
    }
}
