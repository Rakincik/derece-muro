using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MURO.Application.DTOs.Courses;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MURO.Infrastructure.Services;

public class LiveMeetingService : ILiveMeetingService
{
    private readonly MuroDbContext _context;
    private readonly IBbbService _bbbService;
    private readonly INotificationService _notificationService;
    private readonly IGroupAccessService _groupAccess;
    private readonly IConfiguration _config;
    private readonly ICacheService _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public LiveMeetingService(
        MuroDbContext context,
        IBbbService bbbService,
        INotificationService notificationService,
        IGroupAccessService groupAccess,
        IConfiguration config,
        ICacheService cache,
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _bbbService = bbbService;
        _notificationService = notificationService;
        _groupAccess = groupAccess;
        _config = config;
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task<SessionStartResult> StartSessionAsync(Guid courseId, Guid sessionId, Guid moderatorUserId)
    {
        var session = await _context.Sessions
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CourseId == courseId )
            ?? throw new KeyNotFoundException("Oturum bulunamadı.");

        if (session.Status == SessionStatus.Live)
        {
            if (!string.IsNullOrEmpty(session.VideoUrl))
            {
                return new SessionStartResult(session.Id, "", session.VideoUrl, "Live");
            }
            if (!string.IsNullOrEmpty(session.BbbMeetingId))
            {
                var mod = await _context.Users.FindAsync(moderatorUserId)
                    ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
                var modPw = _config["Bbb:DefaultModeratorPw"] ?? "mp";
                var existingJoinUrl = await _bbbService.GetJoinUrlAsync(new BbbJoinOptions(
                    MeetingId: session.BbbMeetingId,
                    FullName: $"{mod.FirstName} {mod.LastName}",
                    Password: modPw, UserId: moderatorUserId, IsModerator: true));
                return new SessionStartResult(session.Id, session.BbbMeetingId, existingJoinUrl, "Live");
            }
            throw new InvalidOperationException("Ders zaten başlatılmış.");
        }

        var moderator = await _context.Users.FindAsync(moderatorUserId)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");



        if (!string.IsNullOrEmpty(session.VideoUrl))
        {
            session.Status = SessionStatus.Live;
            await _context.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync($"courses:");

            var enrolledUserIds = await _context.CourseGroups
                .Where(cg => cg.CourseId == courseId)
                .Join(_context.GroupMembers, cg => cg.GroupId, gm => gm.GroupId, (cg, gm) => gm.UserId)
                .Distinct()
                .ToListAsync();

            if (enrolledUserIds.Any())
            {
                await _notificationService.BulkSendAsync(new Application.DTOs.Notifications.BulkNotificationRequest(
                    enrolledUserIds,
                    "🔴 Canlı Ders Başladı",
                    $"\"{session.Title}\" dersi şu an canlı! Hemen katıl.",
                    $"SessionStarted:{courseId}"
                ));
            }

            return new SessionStartResult(session.Id, "", session.VideoUrl, "Live");
        }

        var meetingId = $"monopol_{session.Id}";
        var attendeePw = _config["Bbb:DefaultAttendeePw"] ?? "ap";
        var moderatorPw = _config["Bbb:DefaultModeratorPw"] ?? "mp";

        await _bbbService.CreateMeetingAsync(new BbbMeetingOptions(
            MeetingId: meetingId,
            MeetingName: session.Title,
            AttendeePw: attendeePw,
            ModeratorPw: moderatorPw,
            RecordingEnabled: session.RecordingEnabled,
            WelcomeMessage: $"Hoş geldiniz! {session.Course.Title} — {session.Title}",
            DurationMinutes: 0, // 0 = Sınırsız (Otomatik kapanmayı engeller)
            LogoutURL: _config["Bbb:Defaults:LogoutURL"],
            SessionId: session.Id.ToString(),
            CourseId: courseId.ToString().ToString()
        ));

        session.BbbMeetingId = meetingId;
        session.Status = SessionStatus.Live;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");

        if (session.RecordingEnabled)
        {
            var existingRecording = await _context.SessionRecordings
                .AnyAsync(r => r.SessionId == session.Id);
            if (!existingRecording)
            {
                _context.SessionRecordings.Add(new SessionRecording
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    Status = MediaStatus.Processing,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
        }

        var moderatorJoinUrl = await _bbbService.GetJoinUrlAsync(new BbbJoinOptions(
            MeetingId: meetingId,
            FullName: $"{moderator.FirstName} {moderator.LastName}",
            Password: moderatorPw,
            UserId: moderatorUserId,
            IsModerator: true
        ));

        var enrolledUserIds2 = await _context.CourseGroups
            .Where(cg => cg.CourseId == courseId)
            .Join(_context.GroupMembers, cg => cg.GroupId, gm => gm.GroupId, (cg, gm) => gm.UserId)
            .Distinct()
            .ToListAsync();

        if (enrolledUserIds2.Any())
        {
            await _notificationService.BulkSendAsync(new Application.DTOs.Notifications.BulkNotificationRequest(
                enrolledUserIds2,
                "🔴 Canlı Ders Başladı",
                $"\"{session.Title}\" dersi şu an canlı! Hemen katıl.",
                $"SessionStarted:{courseId}"
            ));
        }

        return new SessionStartResult(session.Id, meetingId, moderatorJoinUrl, session.Status.ToString());
    }

    public async Task<SessionJoinResult> JoinSessionAsync(
        Guid courseId, Guid sessionId, Guid userId, string fullName, bool checkGroupAccess = false)
    {
        var session = await _context.Sessions
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CourseId == courseId )
            ?? throw new KeyNotFoundException("Oturum bulunamadı.");

        if (session.Status != SessionStatus.Live)
            throw new InvalidOperationException("Ders henüz başlamadı veya sona erdi.");

        if (!string.IsNullOrEmpty(session.VideoUrl))
        {
            if (checkGroupAccess)
            {
                var hasAccess = await _groupAccess.CanAccessCourseAsync(userId, courseId);
                if (!hasAccess)
                    throw new UnauthorizedAccessException("Bu derse erişim yetkiniz yok.");
            }
            else
            {
                var isMember = await _context.Users
                    .AnyAsync(tm => tm.Id == userId && tm.IsActive);
                if (!isMember)
                    throw new UnauthorizedAccessException("Bu derse erişim yetkiniz yok.");
            }

            var user = await _context.Users.FindAsync(userId);
            var isModerator = user?.Role is Domain.Enums.UserRole.Admin or Domain.Enums.UserRole.Instructor;
            return new SessionJoinResult(session.Id, session.VideoUrl, isModerator);
        }

        if (string.IsNullOrEmpty(session.BbbMeetingId))
            throw new InvalidOperationException("BBB meeting bilgisi bulunamadı.");

        if (checkGroupAccess)
        {
            var hasAccess = await _groupAccess.CanAccessCourseAsync(userId, courseId);
            if (!hasAccess)
                throw new UnauthorizedAccessException("Bu derse erişim yetkiniz yok.");
        }
        else
        {
            var isMember = await _context.Users
                .AnyAsync(tm => tm.Id == userId && tm.IsActive);
            if (!isMember)
                throw new UnauthorizedAccessException("Bu derse erişim yetkiniz yok.");
        }

        var user2 = await _context.Users.FindAsync(userId);
        var isModerator2 = user2?.Role is Domain.Enums.UserRole.Admin or Domain.Enums.UserRole.Instructor;
        var password = isModerator2
            ? (_config["Bbb:DefaultModeratorPw"] ?? "mp")
            : (_config["Bbb:DefaultAttendeePw"] ?? "ap");

        var defaultLogoutUrl = _config["Bbb:Defaults:LogoutURL"];
        var studentLogoutUrl = defaultLogoutUrl?.Replace("admin.", "app.").Replace("/admin/dashboard", "/dashboard").Replace("/admin/", "/");
        if (!string.IsNullOrEmpty(studentLogoutUrl))
        {
            studentLogoutUrl = studentLogoutUrl.TrimEnd('/') + $"/{courseId}";
        }
        var logoutUrl = isModerator2 ? defaultLogoutUrl : studentLogoutUrl;

        var joinUrl = await _bbbService.GetJoinUrlAsync(new BbbJoinOptions(
            MeetingId: session.BbbMeetingId,
            FullName: fullName,
            Password: password,
            UserId: userId,
            IsModerator: isModerator2,
            LogoutUrl: logoutUrl
        ));

        // Yoklama kaydı fallback: Webhook bazen çalışmıyor/gecikiyor (özellikle test ortamlarında).
        // Bu sebeple asenkron background task olarak yoklama kaydını Join anında da tetikliyoruz.
        // IServiceScopeFactory kullanılarak DbContext disposed hatasının önüne geçilmiştir.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var attendanceSvc = scope.ServiceProvider.GetRequiredService<ISessionAttendanceService>();
                await attendanceSvc.RecordJoinAsync(session.Id, userId);
            }
            catch (Exception ex)
            {
                // Background task içinde hatayı yut (Loglanabilir)
                Console.WriteLine($"Attendance fallback failed: {ex.Message}");
            }
        });

        return new SessionJoinResult(session.Id, joinUrl, isModerator2);
    }

    public async Task EndSessionAsync(Guid courseId, Guid sessionId)
    {
        var session = await _context.Sessions
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CourseId == courseId )
            ?? throw new KeyNotFoundException("Oturum bulunamadı.");

        if (session.Status != SessionStatus.Live)
            throw new InvalidOperationException("Ders zaten aktif değil.");

        if (!string.IsNullOrEmpty(session.BbbMeetingId))
        {
            var moderatorPw = _config["Bbb:DefaultModeratorPw"] ?? "mp";
            await _bbbService.EndMeetingAsync(session.BbbMeetingId, moderatorPw);
        }

        session.Status = SessionStatus.Ended;

        if (session.RecordingEnabled)
        {
            var alreadyExists = await _context.SessionRecordings.AnyAsync(r => r.SessionId == sessionId);
            if (!alreadyExists)
            {
                _context.SessionRecordings.Add(new SessionRecording
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    Status = MediaStatus.Processing,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");

        var enrolledUserIds = await _context.CourseGroups
            .Where(cg => cg.CourseId == courseId)
            .Join(_context.GroupMembers, cg => cg.GroupId, gm => gm.GroupId, (cg, gm) => gm.UserId)
            .Distinct()
            .ToListAsync();

        if (enrolledUserIds.Any())
        {
            await _notificationService.BulkSendAsync(new Application.DTOs.Notifications.BulkNotificationRequest(
                enrolledUserIds,
                "📚 Ders Sona Erdi",
                $"\"{session.Title}\" dersi sona erdi." + (session.RecordingEnabled ? " Kayıt kısa süre içinde hazır olacak." : ""),
                $"SessionEnded:{courseId}"
            ));
        }
    }
}
