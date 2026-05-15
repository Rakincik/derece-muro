using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Analytics;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// AnalyticsService için unit testler — Dashboard istatistikleri, video izleme
/// stats, finansal işlemler, aktif oturumlar, kurs yoklama raporu,
/// öğrenci skor kartı, öğrenci dashboard.
/// </summary>
public class AnalyticsServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly AnalyticsService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();

    public AnalyticsServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);

        // Cache mock — GetOrSetAsync factory'yi doğrudan çalıştırır
        ICacheService cacheMock = new MURO.Tests.Helpers.DummyCacheService();

        _service = new AnalyticsService(_db, cacheMock);

        // Seed base data
        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Okul", Code = "analytics", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User
        {
            Id = _userId, Email = "student@t.com", FirstName = "Ali", LastName = "Yılmaz",
            PasswordHash = "h", Role = UserRole.Student, IsActive = true, StudentType = StudentType.Active
        });
        _db.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(), UserId = _userId, TenantId = _tenantId,
            Role = UserRole.Student, Status = "active"
        });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Fizik 101", IsPublished = true });
        _db.SaveChanges();
    }

    // ════════════════════════════════════════════════════════════════════════
    // DASHBOARD STATS
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetDashboardStats_EmptyTenant_ShouldReturnZeros()
    {
        var emptyTenantId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = emptyTenantId, Name = "Boş", Code = "empty" });
        await _db.SaveChangesAsync();

        var stats = await _service.GetDashboardStatsAsync(emptyTenantId);

        stats.TotalUsers.Should().Be(0);
        stats.ActiveStudents.Should().Be(0);
        stats.TotalCourses.Should().Be(0);
        stats.TotalExams.Should().Be(0);
    }

    [Fact]
    public async Task GetDashboardStats_WithData_ShouldCountCorrectly()
    {
        // Mevcut veri ile (1 user, 1 course, 1 membership)
        _db.Exams.Add(new Exam { Id = Guid.NewGuid(), TenantId = _tenantId, Title = "Sınav 1", QuestionCount = 10 });
        _db.Groups.Add(new Group { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Grup A" });
        _db.SupportTickets.Add(new SupportTicket { Id = Guid.NewGuid(), TenantId = _tenantId, UserId = _userId, Subject = "Talep", Status = TicketStatus.Open });
        await _db.SaveChangesAsync();

        var stats = await _service.GetDashboardStatsAsync(_tenantId);

        stats.TotalUsers.Should().BeGreaterThanOrEqualTo(1);
        stats.ActiveStudents.Should().BeGreaterThanOrEqualTo(1);
        stats.TotalCourses.Should().Be(1);
        stats.PublishedCourses.Should().Be(1);
        stats.TotalExams.Should().Be(1);
        stats.TotalGroups.Should().Be(1);
        stats.PendingTickets.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboardStats_DemoStudents_ShouldCountSeparately()
    {
        var demoUserId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = demoUserId, Email = "demo@t.com", FirstName = "Demo", LastName = "User",
            PasswordHash = "h", Role = UserRole.Student, IsActive = true, StudentType = StudentType.Demo
        });
        _db.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(), UserId = demoUserId, TenantId = _tenantId,
            Role = UserRole.Student, Status = "active"
        });
        await _db.SaveChangesAsync();

        var stats = await _service.GetDashboardStatsAsync(_tenantId);

        stats.DemoStudents.Should().Be(1);
        stats.ActiveStudents.Should().Be(1);
    }

    // ════════════════════════════════════════════════════════════════════════
    // VIDEO STATS
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetVideoStats_NoVideos_ShouldReturnEmpty()
    {
        var emptyTenantId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = emptyTenantId, Name = "Boş", Code = "empty2" });
        await _db.SaveChangesAsync();

        var stats = await _service.GetVideoStatsAsync(emptyTenantId);
        stats.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVideoStats_WithProgress_ShouldCalculateCorrectly()
    {
        var mediaId = Guid.NewGuid();
        _db.MediaAssets.Add(new MediaAsset { Id = mediaId, Title = "Ders 1", TenantId = _tenantId, Status = MediaStatus.Ready });
        _db.VideoProgresses.Add(new VideoProgress
        {
            Id = Guid.NewGuid(), UserId = _userId, MediaAssetId = mediaId,
            WatchedSeconds = 300, TotalSeconds = 600, LastPosition = 300,
            SkipCount = 2, ReplayCount = 1
        });
        await _db.SaveChangesAsync();

        var stats = await _service.GetVideoStatsAsync(_tenantId);

        stats.Should().HaveCountGreaterThanOrEqualTo(1);
        var s = stats.First(x => x.MediaAssetId == mediaId);
        s.TotalViews.Should().BeGreaterThanOrEqualTo(1);
        s.UniqueViewers.Should().Be(1);
    }

    // ════════════════════════════════════════════════════════════════════════
    // TRANSACTIONS
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTransactions_ShouldFilterByDateRange()
    {
        var now = DateTime.UtcNow;
        _db.Transactions.Add(new Transaction { Id = Guid.NewGuid(), TenantId = _tenantId, Amount = 100, Description = "Ocak", TransactionDate = now.AddDays(-30) });
        _db.Transactions.Add(new Transaction { Id = Guid.NewGuid(), TenantId = _tenantId, Amount = 200, Description = "Bu hafta", TransactionDate = now.AddDays(-3) });
        _db.Transactions.Add(new Transaction { Id = Guid.NewGuid(), TenantId = _tenantId, Amount = 300, Description = "Gelecek", TransactionDate = now.AddDays(30) });
        await _db.SaveChangesAsync();

        var result = await _service.GetTransactionsAsync(_tenantId, now.AddDays(-7), now);

        result.Should().HaveCount(1);
        result[0].Description.Should().Be("Bu hafta");
        result[0].Amount.Should().Be(200);
    }

    [Fact]
    public async Task GetTransactions_DifferentTenant_ShouldReturnEmpty()
    {
        _db.Transactions.Add(new Transaction { Id = Guid.NewGuid(), TenantId = _tenantId, Amount = 100, Description = "Test" });
        await _db.SaveChangesAsync();

        var result = await _service.GetTransactionsAsync(Guid.NewGuid(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(1));
        result.Should().BeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════════
    // ACTIVE SESSIONS (Device)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetActiveSessions_ShouldReturnOnlyActive()
    {
        _db.DeviceSessions.Add(new DeviceSession
        {
            Id = Guid.NewGuid(), UserId = _userId, TenantId = _tenantId,
            DeviceInfo = "iPhone 15", IpAddress = "192.168.1.1", IsActive = true
        });
        _db.DeviceSessions.Add(new DeviceSession
        {
            Id = Guid.NewGuid(), UserId = _userId, TenantId = _tenantId,
            DeviceInfo = "Eski cihaz", IsActive = false, LogoutAt = DateTime.UtcNow.AddHours(-1)
        });
        await _db.SaveChangesAsync();

        var sessions = await _service.GetActiveSessionsAsync(_tenantId);

        sessions.Should().HaveCount(1);
        sessions[0].DeviceInfo.Should().Be("iPhone 15");
        sessions[0].IsActive.Should().BeTrue();
        sessions[0].UserFullName.Should().Contain("Ali");
    }

    // ════════════════════════════════════════════════════════════════════════
    // COURSE ATTENDANCE REPORT
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCourseAttendanceReport_NonExistent_ShouldThrow()
    {
        var act = () => _service.GetCourseAttendanceReportAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Kurs bulunamadı*");
    }

    [Fact]
    public async Task GetCourseAttendanceReport_NoSessions_ShouldReturnEmptyReport()
    {
        var report = await _service.GetCourseAttendanceReportAsync(_tenantId, _courseId);

        report.CourseTitle.Should().Be("Fizik 101");
        report.TotalSessions.Should().Be(0);
        report.Sessions.Should().BeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════════
    // STUDENT SCORECARD
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStudentScorecard_NonExistent_ShouldThrow()
    {
        var act = () => _service.GetStudentScorecardAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Öğrenci bulunamadı*");
    }

    [Fact]
    public async Task GetStudentScorecard_NoActivity_ShouldReturnZeros()
    {
        var scorecard = await _service.GetStudentScorecardAsync(_tenantId, _userId);

        scorecard.FullName.Should().Contain("Ali");
        scorecard.Email.Should().Be("student@t.com");
        scorecard.AttendedSessions.Should().Be(0);
        scorecard.CompletedVideos.Should().Be(0);
        scorecard.SubmittedAssignments.Should().Be(0);
        scorecard.AvgExamScore.Should().Be(0);
    }

    [Fact]
    public async Task GetStudentScorecard_WithVideoProgress_ShouldCalculate()
    {
        var mediaId = Guid.NewGuid();
        _db.MediaAssets.Add(new MediaAsset { Id = mediaId, Title = "V1", TenantId = _tenantId, Status = MediaStatus.Ready });
        _db.VideoProgresses.Add(new VideoProgress
        {
            Id = Guid.NewGuid(), UserId = _userId, MediaAssetId = mediaId,
            WatchedSeconds = 600, TotalSeconds = 600, CompletedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var scorecard = await _service.GetStudentScorecardAsync(_tenantId, _userId);

        scorecard.CompletedVideos.Should().Be(1);
        scorecard.TotalWatchedMinutes.Should().Be(10); // 600/60
    }

    [Fact]
    public async Task GetStudentScorecard_WithExamResults_ShouldCalculateAvg()
    {
        _db.Exams.Add(new Exam { Id = Guid.NewGuid(), TenantId = _tenantId, Title = "S1", QuestionCount = 10 });
        var exam1 = _db.Exams.Local.First();
        _db.ExamResults.Add(new ExamResult { Id = Guid.NewGuid(), ExamId = exam1.Id, UserId = _userId, Score = 80 });
        _db.ExamResults.Add(new ExamResult { Id = Guid.NewGuid(), ExamId = exam1.Id, UserId = _userId, Score = 60 });
        await _db.SaveChangesAsync();

        var scorecard = await _service.GetStudentScorecardAsync(_tenantId, _userId);

        scorecard.AvgExamScore.Should().Be(70); // (80 + 60) / 2
    }

    // ════════════════════════════════════════════════════════════════════════
    // STUDENT DASHBOARD
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStudentDashboard_NoActivity_ShouldReturnZeros()
    {
        var dashboard = await _service.GetStudentDashboardAsync(_tenantId, _userId);

        dashboard.TotalWatchedMinutes.Should().Be(0);
        dashboard.AttendedSessionsThisMonth.Should().Be(0);
        dashboard.CompletedVideos.Should().Be(0);
        dashboard.ConsecutiveDays.Should().Be(0);
        dashboard.ContinueWatching.Should().BeEmpty();
        dashboard.WeeklyActivity.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetStudentDashboard_WeeklyActivity_ShouldHave7Days()
    {
        var dashboard = await _service.GetStudentDashboardAsync(_tenantId, _userId);

        dashboard.WeeklyActivity.Should().HaveCount(7);
        dashboard.WeeklyActivity.Last().IsToday.Should().BeTrue();
    }

    [Fact]
    public async Task GetStudentDashboard_ContinueWatching_ShouldShowUnfinished()
    {
        var mediaId = Guid.NewGuid();
        _db.MediaAssets.Add(new MediaAsset { Id = mediaId, Title = "Yarıda Kalan", TenantId = _tenantId, Status = MediaStatus.Ready });
        _db.VideoProgresses.Add(new VideoProgress
        {
            Id = Guid.NewGuid(), UserId = _userId, MediaAssetId = mediaId,
            WatchedSeconds = 300, TotalSeconds = 600, LastPosition = 300,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var dashboard = await _service.GetStudentDashboardAsync(_tenantId, _userId);

        dashboard.ContinueWatching.Should().HaveCount(1);
        dashboard.ContinueWatching[0].Title.Should().Be("Yarıda Kalan");
        dashboard.ContinueWatching[0].CompletionPercentage.Should().Be(50);
    }

    [Fact]
    public async Task GetStudentDashboard_CompletedVideo_ShouldNotAppearInContinue()
    {
        var mediaId = Guid.NewGuid();
        _db.MediaAssets.Add(new MediaAsset { Id = mediaId, Title = "Tamamlanmış", TenantId = _tenantId, Status = MediaStatus.Ready });
        _db.VideoProgresses.Add(new VideoProgress
        {
            Id = Guid.NewGuid(), UserId = _userId, MediaAssetId = mediaId,
            WatchedSeconds = 600, TotalSeconds = 600, LastPosition = 600,
            CompletedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var dashboard = await _service.GetStudentDashboardAsync(_tenantId, _userId);

        dashboard.ContinueWatching.Should().BeEmpty();
        dashboard.CompletedVideos.Should().Be(1);
    }

    public void Dispose() => _db.Dispose();
}
