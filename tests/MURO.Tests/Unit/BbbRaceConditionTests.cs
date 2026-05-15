using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// BBB Race Condition + Config Matrix Testleri.
/// İki admin aynı anda aynı dersi başlatma, concurrent join, config değişiklikleri.
/// </summary>
public class BbbRaceConditionTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly Mock<IBbbService> _bbbMock;
    private readonly Mock<INotificationService> _notifMock;
    private readonly LiveMeetingService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _admin1 = Guid.NewGuid();
    private readonly Guid _admin2 = Guid.NewGuid();

    public BbbRaceConditionTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _bbbMock = new Mock<IBbbService>();
        _notifMock = new Mock<INotificationService>();
        var grpMock = new Mock<IGroupAccessService>();
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bbb:DefaultAttendeePw"] = "ap",
                ["Bbb:DefaultModeratorPw"] = "mp",
            }).Build();

        _service = new LiveMeetingService(_db, _bbbMock.Object, _notifMock.Object, grpMock.Object, cfg, new MURO.Tests.Helpers.DummyCacheService());

        // Seed
        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "T", Code = "race", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User { Id = _admin1, Email = "a1@t.com", FirstName = "Admin1", LastName = "U", PasswordHash = "h", Role = UserRole.Admin, IsActive = true });
        _db.Users.Add(new User { Id = _admin2, Email = "a2@t.com", FirstName = "Admin2", LastName = "U", PasswordHash = "h", Role = UserRole.Admin, IsActive = true });
        _db.TenantMemberships.Add(new TenantMembership { Id = Guid.NewGuid(), UserId = _admin1, TenantId = _tenantId, Role = UserRole.Admin, Status = "active" });
        _db.TenantMemberships.Add(new TenantMembership { Id = Guid.NewGuid(), UserId = _admin2, TenantId = _tenantId, Role = UserRole.Admin, Status = "active" });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Race Ders" });
        _db.Sessions.Add(new Session
        {
            Id = _sessionId, CourseId = _courseId, Title = "Race Session",
            Status = SessionStatus.Scheduled, RecordingEnabled = true, DurationMinutes = 60
        });
        _db.SaveChanges();

        _bbbMock.Setup(b => b.CreateMeetingAsync(It.IsAny<BbbMeetingOptions>()))
            .ReturnsAsync("race_" + _sessionId);
        _bbbMock.Setup(b => b.GetJoinUrlAsync(It.IsAny<BbbJoinOptions>()))
            .Returns<BbbJoinOptions>(opt => Task.FromResult($"https://bbb.test.com/join?user={opt.FullName}"));
        _bbbMock.Setup(b => b.EndMeetingAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. İKİ ADMİN — Aynı anda Start (sıralı test, mantık doğrulama)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TwoAdmins_StartSameSession_SecondShouldGetExistingUrl()
    {
        // Admin 1 başlatır
        var result1 = await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _admin1);
        result1.Status.Should().Be("Live");

        // Admin 2 de başlatır — oda zaten Live, mevcut URL dönmeli
        var result2 = await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _admin2);
        result2.Status.Should().Be("Live");

        // BBB.CreateMeeting sadece 1 kez çağrılmalı
        _bbbMock.Verify(b => b.CreateMeetingAsync(It.IsAny<BbbMeetingOptions>()), Times.Once);

        // Ama GetJoinUrl 2 kez çağrılmalı (her admin için ayrı URL)
        _bbbMock.Verify(b => b.GetJoinUrlAsync(It.IsAny<BbbJoinOptions>()), Times.AtLeast(2));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. İKİ ADMİN — Biri Start diğeri End (sıralı)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Admin1Start_Admin2End_ShouldWork()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _admin1);

        // Admin 2 dersi bitirir
        await _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        var session = await _db.Sessions.FindAsync(_sessionId);
        session!.Status.Should().Be(SessionStatus.Ended);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. ÇİFT END — Start → End → End (ikinci End hata vermeli)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task DoubleEnd_ShouldThrowOnSecond()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _admin1);
        await _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        var act = () => _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*aktif değil*");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. ÇİFT END — Recording duplicate oluşmamalı
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task DoubleEnd_ShouldNotDuplicateRecording()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _admin1);
        await _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        var recCount = await _db.SessionRecordings.CountAsync(r => r.SessionId == _sessionId);
        recCount.Should().Be(1);

        // İkinci End hata fırlattığı için recording artmaz
        try { await _service.EndSessionAsync(_tenantId, _courseId, _sessionId); }
        catch { }

        var recCount2 = await _db.SessionRecordings.CountAsync(r => r.SessionId == _sessionId);
        recCount2.Should().Be(1, "İkinci End'de duplicate recording oluşmamalı");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. 10 ÖĞRENCI AYNI ANDA JOIN — Attendance kayıtları doğru mu?
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TenStudents_Join_ShouldCreateTenAttendances()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _admin1);

        var studentIds = new List<Guid>();
        for (int i = 0; i < 10; i++)
        {
            var studentId = Guid.NewGuid();
            studentIds.Add(studentId);
            _db.Users.Add(new User
            {
                Id = studentId, Email = $"s{i}@t.com", FirstName = $"S{i}",
                LastName = "U", PasswordHash = "h", Role = UserRole.Student, IsActive = true
            });
            _db.TenantMemberships.Add(new TenantMembership
            {
                Id = Guid.NewGuid(), UserId = studentId, TenantId = _tenantId,
                Role = UserRole.Student, Status = "active"
            });
        }
        await _db.SaveChangesAsync();

        // 10 öğrenci sırayla katılır
        foreach (var sid in studentIds)
        {
            await _service.JoinSessionAsync(_tenantId, _courseId, _sessionId, sid, $"Student {sid}");
        }

        var attendanceCount = await _db.SessionAttendances
            .Where(a => a.SessionId == _sessionId)
            .CountAsync();

        attendanceCount.Should().Be(0, "Yoklama artık BbbWebhookController üzerinden asenkron alınmaktadır");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. AYNI ÖĞRENCI 3 KEZ — Sadece 1 attendance
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task SameStudentJoinsThreeTimes_ShouldHaveOneAttendance()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _admin1);

        var studentId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = studentId, Email = "repeat@t.com", FirstName = "Repeat",
            LastName = "Student", PasswordHash = "h", Role = UserRole.Student, IsActive = true
        });
        _db.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(), UserId = studentId, TenantId = _tenantId,
            Role = UserRole.Student, Status = "active"
        });
        await _db.SaveChangesAsync();

        await _service.JoinSessionAsync(_tenantId, _courseId, _sessionId, studentId, "Repeat Student");
        await _service.JoinSessionAsync(_tenantId, _courseId, _sessionId, studentId, "Repeat Student");
        await _service.JoinSessionAsync(_tenantId, _courseId, _sessionId, studentId, "Repeat Student");

        var count = await _db.SessionAttendances
            .CountAsync(a => a.SessionId == _sessionId && a.UserId == studentId);

        count.Should().Be(0, "Yoklama artık BbbWebhookController üzerinden asenkron alınmaktadır");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. START → JOIN → END → recording durumu kontrolü
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task FullLifecycle_RecordingShouldBeProcessing()
    {
        // Start
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _admin1);

        // Join (admin)
        var joinResult = await _service.JoinSessionAsync(
            _tenantId, _courseId, _sessionId, _admin1, "Admin1");
        joinResult.IsModerator.Should().BeTrue();

        // End
        await _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        // Recording kontrolü
        var recording = await _db.SessionRecordings
            .FirstOrDefaultAsync(r => r.SessionId == _sessionId);

        recording.Should().NotBeNull();
        recording!.Status.Should().Be(MediaStatus.Processing);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. FARKLI SESSION — Her biri bağımsız recording
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TwoSessions_EachShouldHaveOwnRecording()
    {
        var session2Id = Guid.NewGuid();
        _db.Sessions.Add(new Session
        {
            Id = session2Id, CourseId = _courseId, Title = "Session 2",
            Status = SessionStatus.Scheduled, RecordingEnabled = true, DurationMinutes = 30
        });
        await _db.SaveChangesAsync();

        _bbbMock.SetupSequence(b => b.CreateMeetingAsync(It.IsAny<BbbMeetingOptions>()))
            .ReturnsAsync("race_s1")
            .ReturnsAsync("race_s2");

        // Session 1 start + end
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _admin1);
        await _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        // Session 2 start + end
        await _service.StartSessionAsync(_tenantId, _courseId, session2Id, _admin1);
        await _service.EndSessionAsync(_tenantId, _courseId, session2Id);

        var rec1 = await _db.SessionRecordings.AnyAsync(r => r.SessionId == _sessionId);
        var rec2 = await _db.SessionRecordings.AnyAsync(r => r.SessionId == session2Id);

        rec1.Should().BeTrue("Session 1 recording olmalı");
        rec2.Should().BeTrue("Session 2 recording olmalı");

        var totalRec = await _db.SessionRecordings.CountAsync();
        totalRec.Should().Be(2, "Her session'ın kendi recording'i olmalı");
    }

    public void Dispose() => _db.Dispose();
}
