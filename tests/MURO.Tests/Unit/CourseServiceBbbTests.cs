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
/// CourseService BBB entegrasyonu için unit testler.
/// BBB servisi mock'lanır; start/end akışı doğrulanır.
/// </summary>
public class CourseServiceBbbTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly LiveMeetingService _service;
    private readonly Mock<IBbbService> _bbbMock;
    private readonly Mock<INotificationService> _notifMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _moderatorId = Guid.NewGuid();

    public CourseServiceBbbTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);

        _bbbMock = new Mock<IBbbService>();
        _notifMock = new Mock<INotificationService>();
        var grpAccessMock = new Mock<IGroupAccessService>();
        var cfgMock = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bbb:DefaultAttendeePw"] = "ap",
                ["Bbb:DefaultModeratorPw"] = "mp",
            })
            .Build();

        _service = new LiveMeetingService(_db, _bbbMock.Object, _notifMock.Object, grpAccessMock.Object, cfgMock, new MURO.Tests.Helpers.DummyCacheService());

        // Seed data
        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "T", Code = "test", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User
        {
            Id = _moderatorId, Email = "mod@t.com", FirstName = "Mod", LastName = "User",
            PasswordHash = "h", Role = UserRole.Admin, IsActive = true
        });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Ders BBB" });
        _db.Sessions.Add(new Session
        {
            Id = _sessionId, CourseId = _courseId, Title = "Canlı Oturum",
            Status = SessionStatus.Scheduled, RecordingEnabled = true,
            DurationMinutes = 60
        });
        _db.SaveChanges();

        // BBB mock setup
        _bbbMock.Setup(b => b.CreateMeetingAsync(It.IsAny<BbbMeetingOptions>()))
            .ReturnsAsync("test_meetingId");
        _bbbMock.Setup(b => b.GetJoinUrlAsync(It.IsAny<BbbJoinOptions>()))
            .ReturnsAsync("https://bbb.example.com/join/test");
        _bbbMock.Setup(b => b.EndMeetingAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. StartSession → BBB.CreateMeeting çağrılmalı
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartSession_ShouldCallBbbCreateMeeting()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _moderatorId);

        _bbbMock.Verify(b => b.CreateMeetingAsync(It.IsAny<BbbMeetingOptions>()), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. StartSession → Moderator URL dönmeli
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartSession_ShouldReturnModeratorUrl()
    {
        var result = await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _moderatorId);

        result.ModeratorJoinUrl.Should().Contain("bbb.example.com");
        result.Status.Should().Be("Live");
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. EndSession → BBB.EndMeeting çağrılmalı
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EndSession_ShouldCallBbbEndMeeting()
    {
        // Önce başlat
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _moderatorId);

        // Sonra bitir
        await _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        _bbbMock.Verify(b => b.EndMeetingAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. EndSession → Status Ended'a güncellenmeli
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EndSession_ShouldUpdateStatusToEnded()
    {
        await _service.StartSessionAsync(_tenantId, _courseId, _sessionId, _moderatorId);
        await _service.EndSessionAsync(_tenantId, _courseId, _sessionId);

        var session = await _db.Sessions.FindAsync(_sessionId);
        session!.Status.Should().Be(SessionStatus.Ended);
    }

    public void Dispose() => _db.Dispose();
}
