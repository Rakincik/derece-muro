using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Attendance;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class SessionAttendanceService : ISessionAttendanceService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public SessionAttendanceService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    // Eğitmen/Admin: Bir derse ait tüm katılımları özet olarak getirir
    // 🚀 Redis cache: 2 dk TTL
    public async Task<AttendanceSummaryDto> GetAttendanceBySessionAsync(Guid sessionId)
    {
        var cacheKey = $"attendance:session:{sessionId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var session = await _context.Sessions
                .AsNoTracking()
                .Include(s => s.Course)
                .FirstOrDefaultAsync(s => s.Id == sessionId )
                ?? throw new KeyNotFoundException("Oturum bulunamadı.");

            var attendances = await _context.SessionAttendances
                .AsNoTracking()
                .Where(sa => sa.SessionId == sessionId)
                .Include(sa => sa.User)
                .ToListAsync();

            // Kursa atanan toplam öğrenci sayısı
            var totalAssigned = await _context.CourseGroups
                .Where(cg => cg.CourseId == session.CourseId)
                .Join(_context.GroupMembers, cg => cg.GroupId, gm => gm.GroupId, (cg, gm) => gm.UserId)
                .Distinct()
                .CountAsync();

            var attendees = attendances.Select(a => new SessionAttendanceDto(
                a.Id, a.UserId,
                $"{a.User.FirstName} {a.User.LastName}",
                a.JoinedAt, a.LeftAt, a.DurationMinutes,
                true // Sisteme düşen herkes katılmış sayılır
            )).ToList();

            var totalPresent = attendees.Count(a => a.IsPresent);
            var rate = totalAssigned > 0 ? Math.Round((double)totalPresent / totalAssigned * 100, 1) : 0;

            return new AttendanceSummaryDto(
                session.Id, session.Title,
                totalAssigned, totalPresent, rate, attendees);
        }, TimeSpan.FromMinutes(2));
    }

    // Öğrencinin tüm ders katılım geçmişi
    // 🚀 Redis cache: 3 dk TTL
    public async Task<List<MyAttendanceDto>> GetMyAttendanceHistoryAsync(Guid userId)
    {
        var cacheKey = $"attendance:history:{userId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await _context.SessionAttendances
                .AsNoTracking()
                .Where(sa => sa.UserId == userId && !sa.Session.IsDeleted && sa.Session.ScheduledStart.HasValue && sa.Session.Description != "Video (VOD)" && sa.Session.Status != MURO.Domain.Enums.SessionStatus.Cancelled)
                .Include(sa => sa.Session).ThenInclude(s => s.Course)
                .OrderByDescending(sa => sa.JoinedAt)
                .Select(sa => new MyAttendanceDto(
                    sa.SessionId, sa.Session.Title, sa.Session.Course.Title,
                    sa.JoinedAt, sa.LeftAt, sa.DurationMinutes))
                .ToListAsync();
        }, TimeSpan.FromMinutes(3));
    }

    // BBB Webhook: Öğrenci derse katıldı
    public async Task<SessionAttendanceDto> RecordJoinAsync(Guid sessionId, Guid userId)
    {
        // Aynı giriş kaydı yoksa yeni oluştur
        var existing = await _context.SessionAttendances
            .FirstOrDefaultAsync(sa => sa.SessionId == sessionId && sa.UserId == userId);

        if (existing != null)
        {
            // Geri dönen öğrenci: en erken giriş korunur
            var user = await _context.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
            return MapDto(existing, user);
        }

        var attendance = new SessionAttendance
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        };
        _context.SessionAttendances.Add(attendance);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"attendance:");

        var session = await _context.Sessions.FindAsync(sessionId);
        if (session != null)
        {
            await _cache.RemoveAsync($"analytics:courseattendance:{session.CourseId}");
        }

        var u = await _context.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        return MapDto(attendance, u);
    }

    // BBB Webhook: Öğrenci dersten ayrıldı
    public async Task<SessionAttendanceDto> RecordLeaveAsync(Guid sessionId, Guid userId)
    {
        var attendance = await _context.SessionAttendances
            .Include(sa => sa.User)
            .Include(sa => sa.Session)
            .FirstOrDefaultAsync(sa => sa.SessionId == sessionId && sa.UserId == userId)
            ?? throw new KeyNotFoundException("Katılım kaydı bulunamadı.");

        attendance.LeftAt = DateTime.UtcNow;
        attendance.DurationMinutes = (int)(attendance.LeftAt.Value - attendance.JoinedAt).TotalMinutes;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"attendance:");

        if (attendance.Session != null)
        {
            await _cache.RemoveAsync($"analytics:courseattendance:{attendance.Session.CourseId}");
        }

        return MapDto(attendance, attendance.User);
    }

    private static SessionAttendanceDto MapDto(SessionAttendance a, User user) =>
        new(a.Id, a.UserId, $"{user.FirstName} {user.LastName}",
            a.JoinedAt, a.LeftAt, a.DurationMinutes,
            true);
}
