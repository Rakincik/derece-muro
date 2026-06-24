using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Courses;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class CourseService : ICourseService
{
    private readonly MuroDbContext _context;
    private readonly IBbbService _bbbService;
    private readonly INotificationService _notificationService;
    private readonly IGroupAccessService _groupAccess;
    private readonly IConfiguration _config;
    private readonly ICacheService _cache;

    public CourseService(
        MuroDbContext context,
        IBbbService bbbService,
        INotificationService notificationService,
        IGroupAccessService groupAccess,
        IConfiguration config,
        ICacheService cache)
    {
        _context = context;
        _bbbService = bbbService;
        _notificationService = notificationService;
        _groupAccess = groupAccess;
        _config = config;
        _cache = cache;
    }

    // 🚀 Redis cache: sayfalı ders listesi — 5 dk TTL, CRUD sonrası invalidation
    public async Task<PagedResult<CourseListDto>> GetCoursesAsync(
        int page, int pageSize, string? search, string? courseType, bool? isPublished, Guid? instructorId = null)
    {
        // Filtreli sorgularda cache key dinamik — aynı filtre aynı sonucu döner
        var cacheKey = $"courses:list:{page}:{pageSize}:{search ?? ""}:{courseType ?? ""}:{isPublished}:{instructorId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.Courses
                .AsNoTracking()
                .Where(c => true);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(c => c.Title.ToLower().Contains(s) || (c.Description != null && c.Description.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(courseType) && Enum.TryParse<CourseType>(courseType, true, out var ct))
                query = query.Where(c => c.CourseType == ct);

            if (isPublished.HasValue)
                query = query.Where(c => c.IsPublished == isPublished.Value);

            if (instructorId.HasValue)
                query = query.Where(c => c.InstructorId == instructorId.Value);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderBy(c => c.Order).ThenByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CourseListDto(
                    c.Id, c.Title, c.Description, c.ThumbnailUrl,
                    c.CourseType.ToString(), c.IsPublished,
                    c.CourseMedias.Count, c.CourseGroups.Count,
                    c.Order, c.StartDate, c.CreatedAt,
                    c.InstructorId,
                    c.Instructor != null ? c.Instructor.FirstName + " " + c.Instructor.LastName : null))
                .ToListAsync();

            return new PagedResult<CourseListDto>(items, totalCount, page, pageSize, totalPages);
        }, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Student spesifik: sadece kullanıcının aktif grupüne atanmış, yayınlanmış dersler.
    /// </summary>
    public async Task<PagedResult<CourseListDto>> GetCoursesByUserAsync(
        Guid userId, int page, int pageSize, string? search, string? courseType)
    {
        var cacheKey = $"courses:user:{userId}:{page}:{pageSize}:{search ?? ""}:{courseType ?? ""}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var accessibleIds = await _groupAccess.GetAccessibleCourseIdsAsync(userId);

            var query = _context.Courses
                .AsNoTracking()
                .Where(c => c.IsPublished && accessibleIds.Contains(c.Id));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(c => c.Title.ToLower().Contains(s) || (c.Description != null && c.Description.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(courseType) && Enum.TryParse<CourseType>(courseType, true, out var ct))
                query = query.Where(c => c.CourseType == ct);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderBy(c => c.Order).ThenByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CourseListDto(
                    c.Id, c.Title, c.Description, c.ThumbnailUrl,
                    c.CourseType.ToString(), c.IsPublished,
                    c.CourseMedias.Count, c.CourseGroups.Count,
                    c.Order, c.StartDate, c.CreatedAt,
                    c.InstructorId,
                    c.Instructor != null ? c.Instructor.FirstName + " " + c.Instructor.LastName : null))
                .ToListAsync();

            return new PagedResult<CourseListDto>(items, totalCount, page, pageSize, totalPages);
        }, TimeSpan.FromMinutes(1));
    }

    public async Task<CourseDetailDto> GetCourseByIdAsync(Guid courseId, Guid? userId = null)
    {
        var cacheKey = $"courses:detail:{courseId}:{userId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            // Erişim kontrolü: userId verilmişse grup kontrolü yap
            if (userId.HasValue && !await _groupAccess.CanAccessCourseAsync(userId.Value, courseId))
                throw new UnauthorizedAccessException("Bu derse erişim yetkiniz yok.");

            var course = await _context.Courses
                .AsNoTracking()
                .AsSplitQuery() // <-- Cartesian Explosion engelleyici (Performans)
                .Where(c => c.Id == courseId )
                .Include(c => c.Instructor)
                .Include(c => c.Sessions.OrderBy(s => s.Order))
                .Include(c => c.CourseGroups).ThenInclude(cg => cg.Group)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Ders bulunamadı.");

            // BBB ile oturum durumu senkronizasyonu: Live oturumları kontrol et
            var liveSessions = course.Sessions.Where(s => s.Status == SessionStatus.Live && s.BbbMeetingId != null).ToList();
            foreach (var session in liveSessions)
            {
                try
                {
                    var isRunning = await _bbbService.IsMeetingRunningAsync(session.BbbMeetingId!);
                    if (!isRunning)
                    {
                        // BBB'de toplantı bitmiş, DB'yi güncelle
                        var dbSession = await _context.Sessions.FindAsync(session.Id);
                        if (dbSession != null)
                        {
                            dbSession.Status = SessionStatus.Ended;
                            await _context.SaveChangesAsync();
                            session.Status = SessionStatus.Ended; // DTO için de güncelle
                        }
                    }
                }
                catch { /* BBB bağlantı hatası olursa sessizce geç */ }
            }

            return new CourseDetailDto(
                course.Id, course.Title, course.Description, course.ThumbnailUrl,
                course.CourseType.ToString(), course.IsPublished, course.Order, course.StartDate, course.CreatedAt,
                course.Sessions.Select(s => new SessionDto(
                    s.Id, s.Title, s.Description, s.Order, s.VideoUrl,
                    s.DurationMinutes, s.IsFree,
                    s.ScheduledStart, s.ScheduledEnd, s.RecordingEnabled,
                    s.Status.ToString(), s.BbbMeetingId,
                    s.CreatedAt)).ToList(),
                course.CourseGroups.Select(cg => new CourseGroupDto(
                    cg.GroupId, cg.Group.Name, cg.Mode.ToString())).ToList(),
                course.InstructorId,
                course.Instructor != null ? course.Instructor.FirstName + " " + course.Instructor.LastName : null);
        }, TimeSpan.FromMinutes(1));
    }

    public async Task<CourseListDto> CreateCourseAsync(CreateCourseRequest request)
    {
        var maxOrder = await _context.Courses
            .Where(c => true)
            .MaxAsync(c => (int?)c.Order) ?? 0;

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            ThumbnailUrl = request.ThumbnailUrl,
            CourseType = Enum.TryParse<CourseType>(request.CourseType, true, out var ct) ? ct : CourseType.Online,
            Order = request.Order ?? maxOrder + 1,
            StartDate = request.StartDate,
            InstructorId = request.InstructorId
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");

        return new CourseListDto(course.Id, course.Title, course.Description, course.ThumbnailUrl,
            course.CourseType.ToString(), course.IsPublished, 0, 0, course.Order, course.StartDate, course.CreatedAt, course.InstructorId, null);
    }

    public async Task<CourseListDto> UpdateCourseAsync(Guid courseId, UpdateCourseRequest request)
    {
        var course = await _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Sessions)
            .Include(c => c.CourseMedias)
            .Include(c => c.CourseGroups)
            .FirstOrDefaultAsync(c => c.Id == courseId )
            ?? throw new KeyNotFoundException("Ders bulunamadı.");

        if (request.Title != null) course.Title = request.Title;
        if (request.Description != null) course.Description = request.Description;
        if (request.ThumbnailUrl != null) course.ThumbnailUrl = request.ThumbnailUrl;
        if (request.CourseType != null && Enum.TryParse<CourseType>(request.CourseType, true, out var ct))
            course.CourseType = ct;
        if (request.IsPublished.HasValue) course.IsPublished = request.IsPublished.Value;
        if (request.Order.HasValue) course.Order = request.Order.Value;
        if (request.StartDate.HasValue) course.StartDate = request.StartDate;
        if (request.InstructorId != Guid.Empty && request.InstructorId != null) course.InstructorId = request.InstructorId;
        else if (request.InstructorId == Guid.Empty) course.InstructorId = null;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");

        return new CourseListDto(course.Id, course.Title, course.Description, course.ThumbnailUrl,
            course.CourseType.ToString(), course.IsPublished, course.CourseMedias.Count,
            course.CourseGroups.Count, course.Order, course.StartDate, course.CreatedAt,
            course.InstructorId,
            course.Instructor != null ? course.Instructor.FirstName + " " + course.Instructor.LastName : null);
    }

    public async Task DeleteCourseAsync(Guid courseId)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId )
            ?? throw new KeyNotFoundException("Ders bulunamadı.");

        // True Hard Delete: Explicitly delete or nullify all child records first to satisfy Foreign Key constraints
        await _context.CourseStudents.Where(cs => cs.CourseId == courseId).ExecuteDeleteAsync();
        await _context.CourseGroups.Where(cg => cg.CourseId == courseId).ExecuteDeleteAsync();
        await _context.CourseMedias.Where(cm => cm.CourseId == courseId).ExecuteDeleteAsync();
        await _context.CourseMaterials.Where(cm => cm.CourseId == courseId).ExecuteDeleteAsync();
        
        // Assignments & submissions
        var assignmentIds = await _context.Assignments.IgnoreQueryFilters().Where(a => a.CourseId == courseId).Select(a => a.Id).ToListAsync();
        if (assignmentIds.Any())
        {
            await _context.AssignmentSubmissions.Where(s => assignmentIds.Contains(s.AssignmentId)).ExecuteDeleteAsync();
            await _context.Assignments.IgnoreQueryFilters().Where(a => assignmentIds.Contains(a.Id)).ExecuteDeleteAsync();
        }

        // CalendarEvents
        await _context.CalendarEvents.IgnoreQueryFilters().Where(ce => ce.CourseId == courseId).ExecuteDeleteAsync();

        // ExamAssignments
        await _context.ExamAssignments.Where(ea => ea.TargetType == "Course" && ea.TargetId == courseId).ExecuteDeleteAsync();

        // Nullify generic course references
        await _context.MediaAssets.IgnoreQueryFilters().Where(ma => ma.CourseId == courseId).ExecuteUpdateAsync(s => s.SetProperty(m => m.CourseId, (Guid?)null));
        await _context.Podcasts.IgnoreQueryFilters().Where(p => p.CourseId == courseId).ExecuteUpdateAsync(s => s.SetProperty(p => p.CourseId, (Guid?)null));
        await _context.Questions.IgnoreQueryFilters().Where(q => q.CourseId == courseId).ExecuteUpdateAsync(s => s.SetProperty(q => q.CourseId, (Guid?)null));

        // Sessions might have attendances or recordings, so we delete them specifically if needed.
        // Sessions can be soft-deleted, so we ignore query filters to ensure hard deletion.
        var sessionIds = await _context.Sessions.IgnoreQueryFilters().Where(s => s.CourseId == courseId).Select(s => s.Id).ToListAsync();
        if (sessionIds.Any())
        {
            await _context.SessionRecordings.Where(sr => sessionIds.Contains(sr.SessionId)).ExecuteDeleteAsync();
            await _context.SessionAttendances.Where(sa => sessionIds.Contains(sa.SessionId)).ExecuteDeleteAsync();
            await _context.Sessions.IgnoreQueryFilters().Where(s => sessionIds.Contains(s.Id)).ExecuteDeleteAsync();
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");
    }

}
