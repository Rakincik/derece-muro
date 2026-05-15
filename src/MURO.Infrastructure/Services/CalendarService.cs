using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Calendar;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class CalendarService : ICalendarService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public CalendarService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<CalendarEventDto>> GetEventsAsync(Guid tenantId, DateTime from, DateTime to, Guid? groupId, Guid? instructorId = null)
    {
        var cacheKey = $"{tenantId}:calendar:{from:yyyyMMdd}:{to:yyyyMMdd}:{groupId}:{instructorId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.CalendarEvents
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.StartDate >= from && e.EndDate <= to);

            if (groupId.HasValue)
                query = query.Where(e => e.GroupId == groupId);

            if (instructorId.HasValue)
                query = query.Where(e => e.CourseId != null && e.Course.InstructorId == instructorId.Value);

            return await query
                .Include(e => e.Group)
                .Include(e => e.Course)
                .OrderBy(e => e.StartDate)
                .Select(e => new CalendarEventDto(
                    e.Id, e.Title, e.Description, e.EventType,
                    e.StartDate, e.EndDate,
                    e.GroupId, e.Group != null ? e.Group.Name : null,
                    e.CourseId, e.Course != null ? e.Course.Title : null,
                    e.Color, e.CreatedAt))
                .ToListAsync();
        }, TimeSpan.FromMinutes(3));
    }

    public async Task<CalendarEventDto> GetEventByIdAsync(Guid tenantId, Guid eventId)
    {
        return await _context.CalendarEvents
            .AsNoTracking()
            .Where(e => e.Id == eventId && e.TenantId == tenantId)
            .Include(e => e.Group)
            .Include(e => e.Course)
            .Select(e => new CalendarEventDto(
                e.Id, e.Title, e.Description, e.EventType,
                e.StartDate, e.EndDate,
                e.GroupId, e.Group != null ? e.Group.Name : null,
                e.CourseId, e.Course != null ? e.Course.Title : null,
                e.Color, e.CreatedAt))
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Etkinlik bulunamadı.");
    }

    public async Task<CalendarEventDto> CreateEventAsync(Guid tenantId, CreateCalendarEventRequest request, Guid? instructorId = null)
    {
        if (instructorId.HasValue)
        {
            if (!request.CourseId.HasValue)
                throw new UnauthorizedAccessException("Eğitmenler sadece kendilerine ait dersler için etkinlik oluşturabilir.");
            
            var ownsCourse = await _context.Courses.AnyAsync(c => c.Id == request.CourseId && c.TenantId == tenantId && c.InstructorId == instructorId.Value);
            if (!ownsCourse)
                throw new UnauthorizedAccessException("Bu ders üzerinde işlem yapma yetkiniz yok.");
        }

        var ev = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = request.Title,
            Description = request.Description,
            EventType = request.EventType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            GroupId = request.GroupId,
            CourseId = request.CourseId,
            Color = request.Color
        };

        _context.CalendarEvents.Add(ev);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:calendar:");

        return new CalendarEventDto(ev.Id, ev.Title, ev.Description, ev.EventType,
            ev.StartDate, ev.EndDate, ev.GroupId, null, ev.CourseId, null, ev.Color, ev.CreatedAt);
    }

    public async Task<CalendarEventDto> UpdateEventAsync(Guid tenantId, Guid eventId, UpdateCalendarEventRequest request, Guid? instructorId = null)
    {
        var ev = await _context.CalendarEvents.Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Etkinlik bulunamadı.");

        if (instructorId.HasValue)
        {
            if (ev.Course == null || ev.Course.InstructorId != instructorId.Value)
                throw new UnauthorizedAccessException("Bu etkinlik üzerinde işlem yapma yetkiniz yok.");
            
            if (request.CourseId.HasValue && request.CourseId != ev.CourseId)
            {
                var ownsCourse = await _context.Courses.AnyAsync(c => c.Id == request.CourseId && c.TenantId == tenantId && c.InstructorId == instructorId.Value);
                if (!ownsCourse)
                    throw new UnauthorizedAccessException("Yeni seçilen ders üzerinde işlem yapma yetkiniz yok.");
            }
        }

        if (request.Title != null) ev.Title = request.Title;
        if (request.Description != null) ev.Description = request.Description;
        if (request.EventType != null) ev.EventType = request.EventType;
        if (request.StartDate.HasValue) ev.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) ev.EndDate = request.EndDate.Value;
        if (request.GroupId.HasValue) ev.GroupId = request.GroupId;
        if (request.CourseId.HasValue) ev.CourseId = request.CourseId;
        if (request.Color != null) ev.Color = request.Color;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:calendar:");

        return new CalendarEventDto(ev.Id, ev.Title, ev.Description, ev.EventType,
            ev.StartDate, ev.EndDate, ev.GroupId, null, ev.CourseId, null, ev.Color, ev.CreatedAt);
    }

    public async Task DeleteEventAsync(Guid tenantId, Guid eventId, Guid? instructorId = null)
    {
        var ev = await _context.CalendarEvents.Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Etkinlik bulunamadı.");

        if (instructorId.HasValue)
        {
            if (ev.Course == null || ev.Course.InstructorId != instructorId.Value)
                throw new UnauthorizedAccessException("Bu etkinlik üzerinde işlem yapma yetkiniz yok.");
        }

        _context.CalendarEvents.Remove(ev);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:calendar:");
    }
}

