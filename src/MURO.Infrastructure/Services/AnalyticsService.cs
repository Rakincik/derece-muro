using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Analytics;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using StackExchange.Redis;

namespace MURO.Infrastructure.Services;

// Fix #4: IAnalyticsService artık MURO.Application.Interfaces içinde tanımlı.
public class AnalyticsService : IAnalyticsService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly IGroupAccessService _groupAccessService;

    public AnalyticsService(
        MuroDbContext context, 
        ICacheService cache, 
        IConnectionMultiplexer redis,
        IGroupAccessService groupAccessService)
    {
        _context = context;
        _cache = cache;
        _redis = redis;
        _groupAccessService = groupAccessService;
    }

    // ── Admin: Genel dashboard istatistikleri ───────────────────────────────
    // Fix #5: 7 ayrı round-trip yerine paralel Task.WhenAll
    // 🚀 Redis cache: 2 dk TTL — en sık çağrılan endpoint
    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var cacheKey = $"analytics:dashboard";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var memberships = _context.Users
                .Where(tm => tm.IsActive);

            var totalUsers       = await memberships.CountAsync();
            var activeStudents   = await memberships.CountAsync(tm => tm.Role == UserRole.Student && tm.IsActive && tm.StudentType != StudentType.Demo);
            var demoStudents     = await memberships.CountAsync(tm => tm.Role == UserRole.Student && tm.StudentType == StudentType.Demo);
            var totalCourses     = await _context.Courses.CountAsync(c => !c.IsDeleted);
            var publishedCourses = await _context.Courses.CountAsync(c => c.IsPublished && !c.IsDeleted);
            var totalExams       = await _context.Exams.CountAsync(e => !e.IsDeleted);
            var totalAssignments = await _context.Assignments.CountAsync(a => !a.IsDeleted);
            var totalGroups      = await _context.Groups.CountAsync(g => !g.IsDeleted);
            var pendingTickets   = await _context.SupportTickets.CountAsync(t => t.Status == TicketStatus.Open);

            return new DashboardStatsDto(
                totalUsers, activeStudents, demoStudents,
                totalCourses, publishedCourses, totalExams,
                totalAssignments, totalGroups, pendingTickets);
        }, TimeSpan.FromSeconds(15));
    }

    // ── Admin: Video izleme istatistikleri (skip/replay analizi dahil) ──────
    // 🚀 Redis cache: 3 dk TTL
    public async Task<List<VideoWatchStatsDto>> GetVideoStatsAsync()
    {
        var cacheKey = $"analytics:videostats";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await _context.MediaAssets.AsNoTracking()
                .Where(m => true)
                .Select(m => new VideoWatchStatsDto(
                    m.Id, m.Title,
                    m.VideoProgresses.Count,
                    m.VideoProgresses.Select(vp => vp.UserId).Distinct().Count(),
                    m.VideoProgresses.Any()
                        ? m.VideoProgresses.Average(vp => vp.TotalSeconds > 0
                            ? (double)vp.WatchedSeconds / vp.TotalSeconds * 100 : 0)
                        : 0,
                    m.VideoProgresses.Count(vp => vp.CompletedAt != null),
                    m.VideoProgresses.Any() ? m.VideoProgresses.Average(vp => (double)vp.SkipCount) : 0,
                    m.VideoProgresses.Any() ? m.VideoProgresses.Average(vp => (double)vp.ReplayCount) : 0))
                .OrderByDescending(v => v.TotalViews)
                .Take(20)
                .ToListAsync();
        }, TimeSpan.FromMinutes(3));
    }

    // ── Admin: Finansal işlemler ─────────────────────────────────────────────
    // 🚀 Redis cache: 2 dk TTL
    public async Task<List<TransactionDto>> GetTransactionsAsync(DateTime from, DateTime to)
    {
        var cacheKey = $"analytics:transactions:{from:yyyyMMdd}:{to:yyyyMMdd}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await _context.Transactions.AsNoTracking()
                .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
                .OrderByDescending(t => t.TransactionDate)
                .Select(t => new TransactionDto(t.Id, t.Description ?? string.Empty, t.Amount, t.Type, t.TransactionDate))
                .ToListAsync();
        }, TimeSpan.FromMinutes(2));
    }

    // 🟢 Admin: Aktif cihaz oturumları (Real-time Online Presence) 🟢
    // ⚠️ Redis cache sildik çünkü veri anlık değişiyor. DB query'si Redis keys ile optimize edildi.
    public async Task<List<DeviceSessionDto>> GetActiveSessionsAsync()
    {
        var db = _redis.GetDatabase();
        var endpoints = _redis.GetEndPoints();
        var server = _redis.GetServer(endpoints.First());
        
        // Redis'teki heartbeat sinyallerini topla
        var onlineKeys = server.Keys(pattern: "muro:presence:online:*").ToArray();
        
        if (onlineKeys.Length == 0)
            return new List<DeviceSessionDto>();

        var onlineSessionIds = onlineKeys
            .Select(k => 
            {
                var parts = k.ToString().Split(':');
                if (Guid.TryParse(parts.Last(), out var id)) return (Guid?)id;
                return null;
            })
            .Where(id => id.HasValue)
            .Select(id => id.Value)
            .ToList();

        if (!onlineSessionIds.Any())
            return new List<DeviceSessionDto>();

        return await _context.DeviceSessions.AsNoTracking()
            .Where(ds => onlineSessionIds.Contains(ds.Id) && ds.User.Role == UserRole.Student)
            .Include(ds => ds.User)
            .OrderByDescending(ds => ds.LoginAt)
            .Select(ds => new DeviceSessionDto(ds.Id, ds.UserId,
                ds.User.FirstName + " " + ds.User.LastName,
                ds.DeviceInfo, ds.IpAddress, ds.LoginAt, ds.LogoutAt, true))
            .ToListAsync();
    }

    public async Task<List<DeviceSessionDto>> GetRecentSessionsAsync(int days = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        
        // Also fetch active ones from Redis to mark them as IsActive
        var db = _redis.GetDatabase();
        var endpoints = _redis.GetEndPoints();
        var server = _redis.GetServer(endpoints.First());
        var onlineKeys = server.Keys(pattern: "muro:presence:online:*").ToArray();
        
        var onlineSessionIds = onlineKeys
            .Select(k => 
            {
                var parts = k.ToString().Split(':');
                if (Guid.TryParse(parts.Last(), out var id)) return (Guid?)id;
                return null;
            })
            .Where(id => id.HasValue)
            .Select(id => id.Value)
            .ToHashSet();

        return await _context.DeviceSessions.AsNoTracking()
            .Where(ds => ds.LoginAt >= cutoff && ds.User.Role == UserRole.Student)
            .Include(ds => ds.User)
            .OrderByDescending(ds => ds.LoginAt)
            // Limit to max 1000 to prevent huge payloads
            .Take(1000)
            .Select(ds => new DeviceSessionDto(ds.Id, ds.UserId,
                ds.User.FirstName + " " + ds.User.LastName,
                ds.DeviceInfo, ds.IpAddress, ds.LoginAt, ds.LogoutAt, 
                onlineSessionIds.Contains(ds.Id)))
            .ToListAsync();
    }

    // ── Admin: Kurs bazlı devam/yoklama raporu ───────────────────────────────
    // 🚀 Redis cache: 2 dk TTL
    public async Task<CourseAttendanceReportDto> GetCourseAttendanceReportAsync(Guid courseId)
    {
        var cacheKey = $"analytics:courseattendance:{courseId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var course = await _context.Courses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == courseId )
                ?? throw new KeyNotFoundException("Kurs bulunamadı.");

            // Kursa atanmış toplam öğrenci
            var totalEnrolled = await _context.CourseGroups
                .Where(cg => cg.CourseId == courseId && !cg.Group.IsDeleted)
                .Join(_context.GroupMembers, cg => cg.GroupId, gm => gm.GroupId, (cg, gm) => gm.UserId)
                .Distinct()
                .CountAsync();

            // Sadece o kursun aktif müfredatında (CourseMedias tablosunda) olan oturumları baz alıyoruz.
            var activeSessionIds = await _context.CourseMedias.AsNoTracking()
                .Where(cm => cm.CourseId == courseId && cm.SessionId.HasValue)
                .Select(cm => cm.SessionId!.Value)
                .ToListAsync();

            var sessions = await _context.Sessions.AsNoTracking()
                .Where(s => activeSessionIds.Contains(s.Id)
                         && !s.IsDeleted 
                         && (s.ScheduledStart.HasValue || s.Status == SessionStatus.Ended || s.Status == SessionStatus.Live) 
                         && s.Description != "Video (VOD)" 
                         && s.Status != SessionStatus.Cancelled)
                .Include(s => s.SessionAttendances)
                .OrderBy(s => s.ScheduledStart ?? s.CreatedAt)
                .ToListAsync();

            var sessionSummaries = sessions.Select(s =>
            {
                var presentStudents = s.SessionAttendances.Select(a => a.UserId).ToList();
                var presentCount = presentStudents.Count;
                var rate = totalEnrolled > 0 ? Math.Round((double)presentCount / totalEnrolled * 100, 1) : 0;
                return new SessionAttendanceSummaryDto(
                    s.Id, s.Title, s.ScheduledStart ?? s.CreatedAt, presentCount, totalEnrolled, rate, presentStudents);
            }).ToList();

            var avgRate = sessionSummaries.Any()
                ? Math.Round(sessionSummaries.Average(s => s.AttendanceRate), 1) : 0;

            // Risk students: rate < 50%
            // Get per-student attendance rates
            var allStudentIds = await _context.CourseGroups
                .Where(cg => cg.CourseId == courseId && !cg.Group.IsDeleted)
                .Join(_context.GroupMembers, cg => cg.GroupId, gm => gm.GroupId, (cg, gm) => gm.User)
                .Select(u => new EnrolledStudentDto(u.Id, $"{u.FirstName} {u.LastName}"))
                .Distinct()
                .ToListAsync();

            int riskStudentCount = 0;
            if (sessions.Count() > 0)
            {
                foreach (var student in allStudentIds)
                {
                    var attended = sessions.Count(s => s.SessionAttendances.Any(a => a.UserId == student.UserId));
                    var rate = (double)attended / sessions.Count * 100;
                    if (rate < 50) riskStudentCount++;
                }
            }

            return new CourseAttendanceReportDto(
                course.Id, course.Title, sessions.Count, totalEnrolled, avgRate, riskStudentCount, sessionSummaries, allStudentIds);
        }, TimeSpan.FromMinutes(2));
    }

    // ── Admin: Öğrenci skor kartı ────────────────────────────────────────────
    // 🚀 Redis cache: 2 dk TTL + Task.WhenAll paralel sorgu
    public async Task<StudentScorecardDto> GetStudentScorecardAsync(Guid studentId)
    {
        var cacheKey = $"analytics:scorecard:{studentId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == studentId)
                ?? throw new KeyNotFoundException("Öğrenci bulunamadı.");

            var accessibleCourseIds = await _groupAccessService.GetAccessibleCourseIdsAsync(studentId);

            // ── Canlı Ders Devam ──────────────────────────────────────────────
            // Sadece öğrencinin erişebildiği kursların aktif müfredatında (CourseMedias tablosunda) olan oturumları baz alıyoruz.
            var activeSessionIds = await _context.CourseMedias.AsNoTracking()
                .Where(cm => accessibleCourseIds.Contains(cm.CourseId) && cm.SessionId.HasValue)
                .Select(cm => cm.SessionId!.Value)
                .ToListAsync();

            var totalSessions = await _context.Sessions.AsNoTracking()
                .CountAsync(s => activeSessionIds.Contains(s.Id) 
                    && !s.IsDeleted 
                    && ((s.ScheduledStart.HasValue && s.ScheduledStart < DateTime.UtcNow) || s.Status == SessionStatus.Ended || s.Status == SessionStatus.Live) 
                    && s.Description != "Video (VOD)" 
                    && s.Status != SessionStatus.Cancelled);

            var attendedCount = await _context.SessionAttendances.AsNoTracking()
                .CountAsync(sa => sa.UserId == studentId
                    && activeSessionIds.Contains(sa.SessionId)
                    && !sa.Session.IsDeleted
                    && ((sa.Session.ScheduledStart.HasValue && sa.Session.ScheduledStart < DateTime.UtcNow) || sa.Session.Status == SessionStatus.Ended || sa.Session.Status == SessionStatus.Live)
                    && sa.Session.Description != "Video (VOD)"
                    && sa.Session.Status != SessionStatus.Cancelled);

            // ── Video İzleme ──────────────────────────────────────────────────
            var videoProgress = await _context.VideoProgresses.AsNoTracking()
                .Where(vp => vp.UserId == studentId)
                .ToListAsync();

            // FIX: Sadece HLS'i hazır olan gerçek video dosyalarını say (PDF, ses dosyası vb. hariç).
            var totalVideos = await _context.MediaAssets.AsNoTracking()
                .CountAsync(m => m.Status == Domain.Enums.MediaStatus.Ready
                    && m.HlsPath != null
                    && ((m.CourseId != null && accessibleCourseIds.Contains(m.CourseId.Value))
                        || m.CourseMedias.Any(cm => accessibleCourseIds.Contains(cm.CourseId))));

            var submittedAssignments = await _context.AssignmentSubmissions.AsNoTracking()
                .CountAsync(s => s.UserId == studentId);

            var examScores = await _context.ExamResults.AsNoTracking()
                .Where(r => r.UserId == studentId)
                .Select(r => r.Score)
                .ToListAsync();

            // FIX: CompletedAt != null VEYA izleme oranı >= %80 olan videoları "tamamlanmış" say.
            // Böylece videonun %80'ini izleyen ama CompletedAt set edilmemiş öğrenciler de sayılır.
            var completedVideos = videoProgress.Count(vp =>
                vp.CompletedAt != null ||
                (vp.TotalSeconds > 0 && (double)vp.WatchedSeconds / vp.TotalSeconds >= 0.80));
            var totalWatchedMinutes = videoProgress.Sum(vp => vp.WatchedSeconds) / 60;
            var avgScore = examScores.Any() ? Math.Round(examScores.Average(), 1) : 0;

            var videoCompletionRate = totalVideos > 0
                ? Math.Round((double)completedVideos / totalVideos * 100, 1) : 0;
            var attendanceRate = totalSessions > 0
                ? Math.Round((double)attendedCount / totalSessions * 100, 1) : 0;

            return new StudentScorecardDto(
                user.Id, $"{user.FirstName} {user.LastName}", user.Email,
                attendedCount, totalSessions, attendanceRate,
                completedVideos, totalVideos, videoCompletionRate,
                totalWatchedMinutes, submittedAssignments, avgScore);
        }, TimeSpan.FromMinutes(10));
    }

    public async Task<StudentAcademicHistoryDto> GetStudentAcademicHistoryAsync(Guid studentId)
    {
        var cacheKey = $"analytics:academichistory:{studentId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == studentId)
                ?? throw new KeyNotFoundException("Öğrenci bulunamadı.");

            var examsTask = _context.ExamResults.AsNoTracking()
                .Where(r => r.UserId == studentId )
                .Include(r => r.Exam)
                .Select(r => new StudentExamHistoryDto(
                    r.ExamId, r.Exam.Title, r.Score, 
                    r.CorrectCount - (r.WrongCount * r.Exam.WrongPenaltyWeight),
                    r.SubmittedAt))
                .ToListAsync();

            var assignmentsTask = _context.AssignmentSubmissions.AsNoTracking()
                .Where(s => s.UserId == studentId )
                .Include(s => s.Assignment)
                .Select(s => new StudentAssignmentHistoryDto(
                    s.AssignmentId, s.Assignment.Title, s.Score.HasValue ? "Değerlendirildi" : "Bekliyor", s.Score, s.SubmittedAt))
                .ToListAsync();

            await Task.WhenAll(examsTask, assignmentsTask);

            return new StudentAcademicHistoryDto(examsTask.Result, assignmentsTask.Result);
        }, TimeSpan.FromMinutes(2));
    }

    public async Task<List<StudentScorecardDto>> GetStudentScorecardsListAsync()
    {
        var cacheKey = "analytics:scorecards_list";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var studentIds = await _context.Users
                .Where(u => u.IsActive && u.Role == Domain.Enums.UserRole.Student)
                .Select(u => u.Id)
                .ToListAsync();

            var scorecards = new List<StudentScorecardDto>();
            foreach (var chunk in studentIds.Chunk(30))
            {
                var chunkTasks = chunk.Select(sid => GetStudentScorecardAsync(sid));
                scorecards.AddRange(await Task.WhenAll(chunkTasks));
            }
            return scorecards;
        }, TimeSpan.FromMinutes(5));
    }

    // ── Admin: Tüm öğrencilerin toplu karne ortalaması ────────────────────────
    public async Task<ScorecardSummaryDto> GetScorecardSummaryAsync()
    {
        var cacheKey = $"analytics:scorecardsummary";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var studentIds = await _context.Users
                .Where(tm => tm.IsActive && tm.Role == Domain.Enums.UserRole.Student)
                .Select(tm => tm.Id)
                .ToListAsync();

            if (studentIds.Count == 0)
                return new ScorecardSummaryDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            var n = studentIds.Count;
            var scorecards = new List<StudentScorecardDto>();
            
            // To prevent exhausting the DB connection pool, fetch individual scorecards in chunks.
            // Since GetStudentScorecardAsync is heavily cached, subsequent runs will be very fast.
            foreach (var chunk in studentIds.Chunk(30))
            {
                var chunkTasks = chunk.Select(sid => GetStudentScorecardAsync(sid));
                scorecards.AddRange(await Task.WhenAll(chunkTasks));
            }

            return new ScorecardSummaryDto(
                n,
                Math.Round(scorecards.Average(s => s.AttendanceRate), 1),
                Math.Round(scorecards.Average(s => s.VideoCompletionRate), 1),
                Math.Round(scorecards.Average(s => s.AvgExamScore), 1),
                (int)Math.Round(scorecards.Average(s => s.AttendedSessions)),
                (int)Math.Round(scorecards.Average(s => s.TotalSessions)),
                (int)Math.Round(scorecards.Average(s => s.CompletedVideos)),
                (int)Math.Round(scorecards.Average(s => s.TotalVideos)),
                (int)Math.Round(scorecards.Average(s => s.TotalWatchedMinutes)),
                (int)Math.Round(scorecards.Average(s => s.SubmittedAssignments))
            );
        }, TimeSpan.FromMinutes(3));
    }

    // ── Öğrenci: Kendi dashboard istatistikleri ──────────────────────────────
    // Fix #6: Tüm VideoProgress kayıtları belleğe çekilmiyor — SQL'de hesaplanıyor.
    // 🚀 Redis cache: 3 dk TTL — öğrenci bazlı cache
    public async Task<StudentDashboardDto> GetStudentDashboardAsync(Guid userId)
    {
        var cacheKey = $"student:dashboard:{userId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var now = DateTime.UtcNow;
            var weekAgo = now.AddDays(-7);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            // Fix #6: SQL'de topla, belleğe çekme (Tüm Zamanlar)
            var totalWatchedSeconds = await _context.VideoProgresses.AsNoTracking()
                .Where(vp => vp.UserId == userId)
                .SumAsync(vp => (long)vp.WatchedSeconds);
            var totalWatchedMinutes = (int)(totalWatchedSeconds / 60);

            // Bu ay katılım
            var monthlyAttendance = await _context.SessionAttendances.AsNoTracking()
                .Where(sa => sa.UserId == userId && sa.JoinedAt >= monthStart)
                .ToListAsync();
            var attendedThisMonth = monthlyAttendance.Count;

            // Toplam bu aydaki oturum sayısı (SADECE öğrencinin kayıtlı olduğu kurslar için)
            var enrolledCourseIds = await _context.CourseStudents.AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => e.CourseId)
                .ToListAsync();

            var totalSessionsThisMonth = await _context.Sessions.AsNoTracking()
                .Where(s => enrolledCourseIds.Contains(s.CourseId) && s.ScheduledStart >= monthStart && s.ScheduledStart <= now && !s.IsDeleted)
                .CountAsync();

            var attendanceRate = totalSessionsThisMonth > 0
                ? Math.Round((double)attendedThisMonth / totalSessionsThisMonth * 100, 1) : 0;

            // Fix #6: SQL'de say
            var completedVideos = await _context.VideoProgresses
                .CountAsync(vp => vp.UserId == userId
                    && vp.CompletedAt != null);

            // ── Aktif Seri (Consecutif Gün) Hesabı ──────────────────────────────
            var thirtyDaysAgo = now.AddDays(-30);
            var activeDates = await _context.VideoProgresses.AsNoTracking()
                .Where(vp => vp.UserId == userId
                    && vp.UpdatedAt >= thirtyDaysAgo)
                .Select(vp => vp.UpdatedAt.Date)
                .Distinct()
                .ToListAsync();

            var attendanceDates = await _context.SessionAttendances.AsNoTracking()
                .Where(sa => sa.UserId == userId && sa.JoinedAt >= thirtyDaysAgo)
                .Select(sa => sa.JoinedAt.Date)
                .Distinct()
                .ToListAsync();

            var allActiveDates = activeDates.Union(attendanceDates).OrderByDescending(d => d).ToList();
            int consecutiveDays = 0;
            var checkDate = DateTime.UtcNow.Date;
            foreach (var date in allActiveDates)
            {
                if (date == checkDate || date == checkDate.AddDays(-1))
                { consecutiveDays++; checkDate = date; }
                else break;
            }

            // Sınav net ortalaması
            var examResults = await _context.ExamResults.AsNoTracking()
                .Where(r => r.UserId == userId )
                .Select(r => new { r.CorrectCount, r.WrongCount, r.Exam.WrongPenaltyWeight })
                .ToListAsync();
            var avgExamNet = examResults.Any()
                ? Math.Round(examResults.Average(r => r.CorrectCount - (r.WrongCount * r.WrongPenaltyWeight)), 2)
                : 0;

            // "Devam et" listesi — yarıda bırakılan videolar
            var continueWatching = await _context.VideoProgresses.AsNoTracking()
                .Where(vp => vp.UserId == userId
                    && vp.LastPosition > 0
                    && vp.CompletedAt == null)
                .Include(vp => vp.MediaAsset)
                .OrderByDescending(vp => vp.UpdatedAt)
                .Take(5)
                .Select(vp => new ResumeVideoDto(
                    vp.MediaAssetId, vp.MediaAsset.Title, vp.MediaAsset.ThumbnailPath,
                    vp.LastPosition, vp.TotalSeconds,
                    vp.TotalSeconds > 0 ? Math.Round((double)vp.WatchedSeconds / vp.TotalSeconds * 100, 1) : 0))
                .ToListAsync();

            // ── Haftalık Aktivite (Son 7 gün) ──────────────────────────────────
            var turkishDays = new[] { "Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt" };
            var sevenDaysAgo = now.Date.AddDays(-6);
            var dailyMinutes = await _context.VideoProgresses.AsNoTracking()
                .Where(vp => vp.UserId == userId
                    && vp.UpdatedAt >= sevenDaysAgo)
                .GroupBy(vp => vp.UpdatedAt.Date)
                .Select(g => new { Date = g.Key, Minutes = (int)(g.Sum(vp => (long)vp.WatchedSeconds) / 60) })
                .ToListAsync();

            var weeklyActivity = Enumerable.Range(0, 7).Select(i =>
            {
                var date = sevenDaysAgo.AddDays(i);
                var mins = dailyMinutes.FirstOrDefault(d => d.Date == date)?.Minutes ?? 0;
                return new WeeklyActivityDto(turkishDays[(int)date.DayOfWeek], mins, date == now.Date);
            }).ToList();

            return new StudentDashboardDto(
                totalWatchedMinutes, attendedThisMonth, totalSessionsThisMonth,
                attendanceRate, completedVideos, consecutiveDays, avgExamNet, continueWatching, weeklyActivity);
        }, TimeSpan.FromMinutes(3));
    }

    // ── Admin Dashboard (Charts & KPIs) ──────────────────────────────────────
    public async Task<AdminDashboardDto> GetAdminDashboardAsync()
    {
        var cacheKey = $"analytics:admin_dashboard";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var now = DateTime.UtcNow;
            var sevenDaysAgo = now.Date.AddDays(-6);
            
            // 1. TotalVideosWatched
            var totalVideosWatched = await _context.VideoProgresses.AsNoTracking().CountAsync(vp => vp.CompletedAt != null);

            // 2. WeeklyActivity
            var turkishDays = new[] { "Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt" };
            
            var videoStatsThisWeek = await _context.VideoProgresses.AsNoTracking()
                .Where(vp => vp.UpdatedAt >= sevenDaysAgo)
                .GroupBy(vp => vp.UpdatedAt.Date)
                .Select(g => new { Date = g.Key, VideoMinutes = (int)(g.Sum(vp => (long)vp.WatchedSeconds) / 60) })
                .ToListAsync();

            var sessionStatsThisWeek = await _context.SessionAttendances.AsNoTracking()
                .Where(sa => sa.JoinedAt >= sevenDaysAgo)
                .GroupBy(sa => sa.JoinedAt.Date)
                .Select(g => new { Date = g.Key, Sessions = g.Count() })
                .ToListAsync();

            var weeklyActivity = Enumerable.Range(0, 7).Select(i =>
            {
                var date = sevenDaysAgo.AddDays(i);
                var mins = videoStatsThisWeek.FirstOrDefault(d => d.Date == date)?.VideoMinutes ?? 0;
                var sessions = sessionStatsThisWeek.FirstOrDefault(d => d.Date == date)?.Sessions ?? 0;
                return new AdminWeeklyActivityDto(turkishDays[(int)date.DayOfWeek], mins, sessions);
            }).ToList();

            // 3. TopCourses
            var topCourses = await _context.Courses.AsNoTracking()
                .Select(c => new AdminTopCourseDto(
                    c.Id,
                    c.Title,
                    c.CourseGroups.SelectMany(cg => cg.Group.Members).Select(m => m.UserId).Distinct().Count(),
                    0
                ))
                .OrderByDescending(c => c.StudentCount)
                .Take(5)
                .ToListAsync();

            // 4. TopStudents
            var topStudents = await _context.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Student)
                .Select(u => new AdminTopStudentDto(
                    u.Id,
                    u.FirstName + " " + u.LastName,
                    u.ExamResults.Any() ? u.ExamResults.Average(er => er.Score) : 0
                ))
                .OrderByDescending(u => u.Score)
                .Take(5)
                .ToListAsync();

            return new AdminDashboardDto(
                totalVideosWatched,
                weeklyActivity,
                topCourses,
                topStudents
            );
        }, TimeSpan.FromSeconds(15));
    }
}
