using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Courses;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class CourseSessionService : ICourseSessionService
{
    private readonly MuroDbContext _context;
    private readonly IGroupAccessService _groupAccess;
    private readonly ICacheService _cache;
    private readonly IServiceProvider _serviceProvider;

    public CourseSessionService(
        MuroDbContext context,
        IGroupAccessService groupAccess,
        ICacheService cache,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _groupAccess = groupAccess;
        _cache = cache;
        _serviceProvider = serviceProvider;
    }

    public async Task<SessionDto> CreateSessionAsync(Guid courseId, CreateSessionRequest request)
    {
        var course = await _context.Courses
            .Include(c => c.Sessions)
            .FirstOrDefaultAsync(c => c.Id == courseId )
            ?? throw new KeyNotFoundException("Ders bulunamadı.");

        var maxOrder = course.Sessions.Any() ? course.Sessions.Max(s => s.Order) : 0;

        if (request.ScheduledStart.HasValue)
        {
            var start = request.ScheduledStart.Value;
            var end = request.ScheduledEnd ?? start.AddMinutes(request.DurationMinutes ?? 60);
            await CheckSessionConflictAsync(courseId, null, start, end);
        }

        var session = new Session
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Title = request.Title,
            Description = request.Description,
            Order = request.Order ?? maxOrder + 1,
            VideoUrl = request.VideoUrl,
            DurationMinutes = request.DurationMinutes,
            IsFree = request.IsFree,
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = request.ScheduledEnd,
            RecordingEnabled = request.RecordingEnabled,
            Status = SessionStatus.Scheduled
        };

        _context.Sessions.Add(session);

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");
        await _cache.RemoveAsync($"analytics:courseattendance:{courseId}");

        return MapSessionDto(session);
    }

    public async Task<SessionDto> UpdateSessionAsync(Guid courseId, Guid sessionId, UpdateSessionRequest request)
    {
        var session = await _context.Sessions
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CourseId == courseId )
            ?? throw new KeyNotFoundException("Oturum bulunamadı.");

        if (request.ScheduledStart.HasValue || request.DurationMinutes.HasValue || request.ScheduledEnd.HasValue)
        {
            var start = request.ScheduledStart ?? session.ScheduledStart;
            if (start.HasValue)
            {
                var dur = request.DurationMinutes ?? session.DurationMinutes ?? 60;
                var end = request.ScheduledEnd ?? session.ScheduledEnd ?? start.Value.AddMinutes(dur);
                await CheckSessionConflictAsync(courseId, session.Id, start.Value, end);
            }
        }

        if (request.Title != null) session.Title = request.Title;
        if (request.Description != null) session.Description = request.Description;
        if (request.Order.HasValue) session.Order = request.Order.Value;
        if (request.VideoUrl != null) session.VideoUrl = request.VideoUrl;
        if (request.DurationMinutes.HasValue) session.DurationMinutes = request.DurationMinutes;
        if (request.IsFree.HasValue) session.IsFree = request.IsFree.Value;
        if (request.ScheduledStart.HasValue) session.ScheduledStart = request.ScheduledStart;
        if (request.ScheduledEnd.HasValue) session.ScheduledEnd = request.ScheduledEnd;
        if (request.RecordingEnabled.HasValue) session.RecordingEnabled = request.RecordingEnabled.Value;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");
        await _cache.RemoveAsync($"analytics:courseattendance:{courseId}");

        return MapSessionDto(session);
    }

    private async Task CheckSessionConflictAsync(Guid courseId, Guid? sessionId, DateTime startT, DateTime endT)
    {
        var potentialConflicts = await _context.Sessions
            .Include(s => s.Course)
            .Where(s => s.CourseId == courseId && s.Id != sessionId && s.ScheduledStart.HasValue)
            .Where(s => s.Status != Domain.Enums.SessionStatus.Ended)
            .Where(s => s.ScheduledStart >= startT.AddDays(-1) && s.ScheduledStart <= startT.AddDays(1))
            .ToListAsync();

        var conflict = potentialConflicts.FirstOrDefault(s =>
        {
            var sStart = s.ScheduledStart!.Value;
            var sEnd = s.ScheduledEnd ?? sStart.AddMinutes(s.DurationMinutes ?? 60);
            return startT < sEnd && sStart < endT;
        });

        if (conflict != null)
            throw new InvalidOperationException($"Oturum çakışması: Bu saatlerde '{conflict.Title}' (Ders: {conflict.Course.Title}) adlı oturum çakışmaktadır.");
    }

    public async Task DeleteSessionAsync(Guid courseId, Guid sessionId)
    {
        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CourseId == courseId)
            ?? throw new KeyNotFoundException("Oturum bulunamadı.");

        _context.Sessions.Remove(session);

        var courseMedia = await _context.CourseMedias.FirstOrDefaultAsync(cm => cm.SessionId == sessionId);
        if (courseMedia != null)
        {
            _context.CourseMedias.Remove(courseMedia);
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");
        await _cache.RemoveAsync($"analytics:courseattendance:{courseId}");
    }

    public async Task ReorderSessionsAsync(Guid courseId, List<Guid> sessionIds)
    {
        var sessions = await _context.Sessions
            .Where(s => s.CourseId == courseId)
            .ToListAsync();

        for (var i = 0; i < sessionIds.Count; i++)
        {
            var session = sessions.FirstOrDefault(s => s.Id == sessionIds[i]);
            if (session != null) session.Order = i + 1;
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");
        await _cache.RemoveAsync($"analytics:courseattendance:{courseId}");
    }

    public async Task<List<UpcomingSessionDto>> GetUpcomingSessionsAsync()
    {
        var sessions = await _context.Sessions
            .Where(s => s.Course.IsPublished && (s.ScheduledStart > DateTime.UtcNow || s.Status == SessionStatus.Live))
            .Include(s => s.Course)
            .OrderBy(s => s.Status == SessionStatus.Live ? 0 : 1).ThenBy(s => s.ScheduledStart)
            .ToListAsync();

        return await SyncAndMapUpcomingSessions(sessions);
    }

    public async Task<List<UpcomingSessionDto>> GetUpcomingSessionsByUserAsync(Guid userId)
    {
        var accessibleIds = await _groupAccess.GetAccessibleCourseIdsAsync(userId);

        var sessions = await _context.Sessions
            .Where(s => s.Course.IsPublished
                        && accessibleIds.Contains(s.CourseId)
                        && (s.ScheduledStart > DateTime.UtcNow || s.Status == SessionStatus.Live))
            .Include(s => s.Course)
            .OrderBy(s => s.Status == SessionStatus.Live ? 0 : 1).ThenBy(s => s.ScheduledStart)
            .ToListAsync();

        return await SyncAndMapUpcomingSessions(sessions);
    }

    private async Task<List<UpcomingSessionDto>> SyncAndMapUpcomingSessions(List<Session> sessions)
    {
        var result = new List<UpcomingSessionDto>();
        bool dbChanged = false;

        using var scope = _serviceProvider.CreateScope();
        var bbbService = scope.ServiceProvider.GetService<IBbbService>();

        foreach (var s in sessions)
        {
            if (s.Status == SessionStatus.Live && s.BbbMeetingId != null && bbbService != null)
            {
                try
                {
                    var isRunning = await bbbService.IsMeetingRunningAsync(s.BbbMeetingId);
                    if (!isRunning)
                    {
                        s.Status = SessionStatus.Ended;
                        dbChanged = true;
                    }
                }
                catch { /* Ignore BBB errors */ }
            }

            // Only add if it's truly upcoming OR still genuinely live
            if (s.ScheduledStart > DateTime.UtcNow || s.Status == SessionStatus.Live)
            {
                result.Add(new UpcomingSessionDto(
                    s.Id, s.Title, s.Description, s.Order,
                    s.VideoUrl, s.DurationMinutes, s.IsFree,
                    s.ScheduledStart, s.ScheduledEnd,
                    s.RecordingEnabled, s.Status.ToString(), s.BbbMeetingId,
                    s.CreatedAt,
                    s.CourseId, s.Course.Title));
            }
        }

        if (dbChanged)
        {
            await _context.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync("courses:");
        }

        return result;
    }


    public async Task<SessionDto> CreateVodSessionAsync(Guid courseId, string title, string filePath, int? durationSeconds = null)
    {
        var course = await _context.Courses
            .Include(c => c.Sessions)
            .FirstOrDefaultAsync(c => c.Id == courseId )
            ?? throw new KeyNotFoundException("Ders bulunamadı.");

        var maxOrder = course.Sessions.Any() ? course.Sessions.Max(s => s.Order) : 0;

        var session = new Session
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Title = title,
            Description = "Video (VOD)",
            Order = maxOrder + 1,
            ScheduledStart = DateTime.UtcNow,
            RecordingEnabled = true,
            Status = SessionStatus.Ended,
            CreatedAt = DateTime.UtcNow,
            DurationMinutes = durationSeconds.HasValue ? (int)Math.Ceiling(durationSeconds.Value / 60.0) : null
        };

        var mediaAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            Title = title,
            FilePath = filePath,
            Status = MediaStatus.Uploading,
            CreatedAt = DateTime.UtcNow,
            CourseId = courseId,
            DurationSeconds = durationSeconds
        };

        var recording = new SessionRecording
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Status = MediaStatus.Processing, // Will be updated by UploadProcessingJob when MediaAsset is Ready
            MediaAssetId = mediaAsset.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.Sessions.Add(session);
        _context.MediaAssets.Add(mediaAsset);
        _context.SessionRecordings.Add(recording);
        
        var courseMediaMaxOrder = await _context.CourseMedias
            .Where(cm => cm.CourseId == courseId)
            .MaxAsync(cm => (int?)cm.OrderIndex) ?? -1;

        _context.CourseMedias.Add(new CourseMedia
        {
            CourseId = courseId,
            SessionId = session.Id,
            OrderIndex = courseMediaMaxOrder + 1
        });

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");
        await _cache.RemoveAsync($"analytics:courseattendance:{courseId}");

        return MapSessionDto(session);
    }

    private static SessionDto MapSessionDto(Session s) => new(
        s.Id, s.Title, s.Description, s.Order,
        s.VideoUrl, s.DurationMinutes, s.IsFree,
        s.ScheduledStart, s.ScheduledEnd, s.RecordingEnabled,
        s.Status.ToString(), s.BbbMeetingId,
        s.CreatedAt);
}
