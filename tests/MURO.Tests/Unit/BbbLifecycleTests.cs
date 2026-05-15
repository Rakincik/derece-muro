using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using MURO.Application.DTOs.Courses;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// BBB Yaşam Döngüsü — Session Start → Join → End akışlarının uçtan uca testi.
/// CourseService üzerinden BBB entegrasyonunun doğru çalıştığını doğrular.
/// </summary>
public class BbbLifecycleTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly LiveMeetingService _service;
    private readonly Mock<IBbbService> _bbbMock;
    private readonly Mock<INotificationService> _notifMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();

    public BbbLifecycleTests()
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
                ["Bbb:DefaultAttendeePw"] = "attendee-pw",
                ["Bbb:DefaultModeratorPw"] = "moderator-pw",
            })
            .Build();

        _service = new LiveMeetingService(_db, _bbbMock.Object, _notifMock.Object, grpMock.Object, cfg, new MURO.Tests.Helpers.DummyCacheService());

        // Seed
        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Uni", Code = "testuni", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User { Id = _adminId, Email = "admin@t.com", FirstName = "Admin", LastName = "User", PasswordHash = "h", Role = UserRole.Admin, IsActive = true });
        _db.Users.Add(new User { Id = _studentId, Email = "student@t.com", FirstName = "Student", LastName = "User", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.TenantMemberships.Add(new TenantMembership { Id = Guid.NewGuid(), UserId = _adminId, TenantId = _tenantId, Role = UserRole.Admin, Status = "active" });
        _db.TenantMemberships.Add(new TenantMembership { Id = Guid.NewGuid(), UserId = _studentId, TenantId = _tenantId, Role = UserRole.Student, Status = "active" });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Fizik 101" });
        _db.Sessions.Add(new Session
        {
            Id = _sessionId, CourseId = _courseId, Title = "Canlı Ders 1",
            Status = SessionStatus.Scheduled, RecordingEnabled = true, DurationMinutes = 90
        });
        _db.SaveChanges();

        // BBB mock defaults
        _bbbMock.Setup(b => b.CreateMeetingAsync(It.IsAny<BbbMeetingOptions>()))
            .ReturnsAsync("testuni_" + _sessionId);
        _bbbMock.Setup(b => b.GetJoinUrlAsync(It.IsAny<BbbJoinOptions>()))
            .Returns<BbbJoinOptions>(opt =>
                Task.FromResult($"https://bbb.test.com/join?meetingID={opt.MeetingId}&fullName={opt.FullName}"));
        _bbbMock.Setup(b => b.EndMeetingAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    // ── 1. Start → MeetingId {TenantCode}_{SessionId} olmalı ────────────────
    [Fact]
    public async Task StartSession_ShouldCreateMeetingWithCorrectId()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);

        _bbbMock.Verify(b => b.CreateMeetingAsync(
            It.Is<BbbMeetingOptions>(o =>
                o.MeetingId == $"testuni_{_sessionId}" &&
                o.RecordingEnabled == true &&
                o.DurationMinutes == 90
            )), Times.Once);
    }

    // ── 2. Start → Session Status Live olmalı ───────────────────────────────
    [Fact]
    public async Task StartSession_ShouldSetStatusToLive()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);

        var session = await _db.Sessions.FindAsync(_sessionId);
        session!.Status.Should().Be(SessionStatus.Live);
        session.BbbMeetingId.Should().Be($"testuni_{_sessionId}");
    }

    // ── 3. Start → Moderatör URL dönmeli ────────────────────────────────────
    [Fact]
    public async Task StartSession_ShouldReturnModeratorJoinUrl()
    {
        var result = await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);

        result.ModeratorJoinUrl.Should().Contain("bbb.test.com/join");
        result.ModeratorJoinUrl.Should().Contain("Admin");
        result.Status.Should().Be("Live");
    }

    // ── 4. Zaten Live → Aynı URL tekrar dönmeli (reuse) ────────────────────
    [Fact]
    public async Task StartSession_AlreadyLive_ShouldReturnExistingUrl()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);
        var result2 = await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);

        result2.Status.Should().Be("Live");
        // CreateMeeting sadece 1 kez çağrılmalı (ilk seferde)
        _bbbMock.Verify(b => b.CreateMeetingAsync(It.IsAny<BbbMeetingOptions>()), Times.Once);
    }

    // ── 5. Join → Student attendee olarak katılmalı ─────────────────────────
    [Fact]
    public async Task JoinSession_Student_ShouldGetAttendeeUrl()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);

        var result = await _service.JoinSessionAsync(
            _tenantId, _courseId, _sessionId, _studentId, "Student User");

        result.JoinUrl.Should().Contain("Student");
        result.IsModerator.Should().BeFalse();

        // Attendance kaydı oluşturulmuş mu?
        var attendance = await _db.SessionAttendances
            .AnyAsync(a => a.SessionId == _sessionId && a.UserId == _studentId);
        attendance.Should().BeFalse(); // Yoklama artık webhook üzerinden alınıyor
    }

    // ── 6. Join → Session henüz başlamadı → Hata ────────────────────────────
    [Fact]
    public async Task JoinSession_NotLive_ShouldThrow()
    {
        var act = () => _service.JoinSessionAsync(
            _tenantId, _courseId, _sessionId, _studentId, "Student User");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*başlamadı*");
    }

    // ── 7. Join → Duplicate attendance kaydı oluşmamalı ─────────────────────
    [Fact]
    public async Task JoinSession_Twice_ShouldNotDuplicateAttendance()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);
        await _service.JoinSessionAsync(_tenantId, _courseId, _sessionId, _studentId, "Student User");
        await _service.JoinSessionAsync(_tenantId, _courseId, _sessionId, _studentId, "Student User");

        var count = await _db.SessionAttendances
            .CountAsync(a => a.SessionId == _sessionId && a.UserId == _studentId);
        count.Should().Be(0); // Yoklama artık webhook üzerinden alınıyor
    }

    // ── 8. End → BBB.EndMeeting çağrılmalı ──────────────────────────────────
    [Fact]
    public async Task EndSession_ShouldCallBbbEnd()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);
        await _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        _bbbMock.Verify(b => b.EndMeetingAsync(
            It.Is<string>(s => s.Contains("testuni_")),
            It.IsAny<string>()), Times.Once);
    }

    // ── 9. End → Status Ended olmalı ────────────────────────────────────────
    [Fact]
    public async Task EndSession_ShouldSetStatusToEnded()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);
        await _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        var session = await _db.Sessions.FindAsync(_sessionId);
        session!.Status.Should().Be(SessionStatus.Ended);
    }

    // ── 10. End → RecordingEnabled → SessionRecording oluşmalı ──────────────
    [Fact]
    public async Task EndSession_WithRecording_ShouldCreateSessionRecording()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);
        await _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        var recording = await _db.SessionRecordings
            .FirstOrDefaultAsync(r => r.SessionId == _sessionId);
        recording.Should().NotBeNull();
        recording!.Status.Should().Be(MediaStatus.Processing);
    }

    // ── 11. Var olmayan session → Start hata ────────────────────────────────
    [Fact]
    public async Task StartSession_NonExistent_ShouldThrow()
    {
        var act = () => _service.StartSessionAsync(_tenantId, _courseId, Guid.NewGuid(), _adminId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── 12. Admin Join → IsModerator=true olmalı ────────────────────────────
    [Fact]
    public async Task JoinSession_Admin_ShouldBeModerator()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _adminId);

        var result = await _service.JoinSessionAsync(
            _tenantId, _courseId, _sessionId, _adminId, "Admin User");

        result.IsModerator.Should().BeTrue();
    }

    public void Dispose() => _db.Dispose();
}
