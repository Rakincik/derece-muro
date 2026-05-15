using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Application.DTOs.Admin;

namespace MURO.Infrastructure.Services;

public class AdminAnalyticsService : IAdminAnalyticsService
{
    private readonly MuroDbContext _db;
    private readonly IBbbService _bbb;
    private readonly ILogger<AdminAnalyticsService> _logger;
    private readonly IConfiguration _config;

    public AdminAnalyticsService(MuroDbContext db, IBbbService bbb, ILogger<AdminAnalyticsService> logger, IConfiguration config)
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

    // D) ANALÄ°TÄ°K â€” Tarihsel Veriler
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>Son N gÃ¼nÃ¼n oturum istatistikleri (saatlik)</summary>
    public async Task<(int, object?)> GetSessionHistory(int days = 7)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var sessions = await _db.Sessions
            .AsNoTracking()
            .Where(s => s.ScheduledStart >= since)
            .Select(s => new
            {
                Date = s.ScheduledStart!.Value.Date,
                Hour = s.ScheduledStart!.Value.Hour,
                Status = s.Status.ToString(),
                s.DurationMinutes,
                ParticipantCount = s.SessionAttendances.Count(),
            })
            .ToListAsync();

        // GÃ¼nlÃ¼k Ã¶zet
        var dailyStats = sessions
            .GroupBy(s => s.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                total = g.Count(),
                live = g.Count(s => s.Status == "Live"),
                ended = g.Count(s => s.Status == "Ended"),
                totalParticipants = g.Sum(s => s.ParticipantCount),
                avgDuration = g.Where(s => s.DurationMinutes.HasValue).Select(s => s.DurationMinutes!.Value).DefaultIfEmpty(0).Average(),
            })
            .ToList();

        // Saatlik daÄŸÄ±lÄ±m (tÃ¼m dÃ¶nem)
        var hourlyDistribution = sessions
            .GroupBy(s => s.Hour)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                hour = g.Key,
                count = g.Count(),
                participants = g.Sum(s => s.ParticipantCount),
            })
            .ToList();

        return Ok(new
        {
            days,
            totalSessions = sessions.Count,
            totalParticipants = sessions.Sum(s => s.ParticipantCount),
            dailyStats,
            hourlyDistribution,
        });
    }

    /// <summary>HaftalÄ±k Ä±sÄ± haritasÄ± â€” gÃ¼n Ã— saat matris</summary>
    public async Task<(int, object?)> GetSessionHeatmap(int weeks = 4)
    {
        weeks = Math.Clamp(weeks, 1, 12);
        var since = DateTime.UtcNow.Date.AddDays(-weeks * 7);

        var sessions = await _db.Sessions
            .AsNoTracking()
            .Where(s => s.ScheduledStart >= since)
            .Select(s => new
            {
                DayOfWeek = (int)s.ScheduledStart!.Value.DayOfWeek,
                Hour = s.ScheduledStart!.Value.Hour,
                ParticipantCount = s.SessionAttendances.Count(),
            })
            .ToListAsync();

        // 7 gÃ¼n Ã— 24 saat matris
        var heatmap = sessions
            .GroupBy(s => new { s.DayOfWeek, s.Hour })
            .Select(g => new
            {
                day = g.Key.DayOfWeek,
                hour = g.Key.Hour,
                sessions = g.Count(),
                participants = g.Sum(s => s.ParticipantCount),
            })
            .OrderBy(h => h.day)
            .ThenBy(h => h.hour)
            .ToList();

        return (200, new { weeks, heatmap });
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
}
