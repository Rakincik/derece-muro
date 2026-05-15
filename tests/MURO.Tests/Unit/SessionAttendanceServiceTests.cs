using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// SessionAttendanceService için unit testler — Yoklama kayıt ve hesaplama.
/// </summary>
public class SessionAttendanceServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly SessionAttendanceService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _courseId;
    private readonly Guid _sessionId;
    private readonly Guid _studentId;

    public SessionAttendanceServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _service = new SessionAttendanceService(_db, new MURO.Tests.Helpers.DummyCacheService());

        // Sabit test verileri oluştur
        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "test", CreatedAt = DateTime.UtcNow });

        _courseId = Guid.NewGuid();
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Ders 1", IsPublished = true });

        _sessionId = Guid.NewGuid();
        _db.Sessions.Add(new Session
        {
            Id = _sessionId, CourseId = _courseId, Title = "Oturum 1",
            Status = SessionStatus.Ended, ScheduledStart = DateTime.UtcNow.AddHours(-2)
        });

        _studentId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = _studentId, Email = "student@test.com", FirstName = "Ali", LastName = "Yılmaz",
            PasswordHash = "hash", Role = UserRole.Student, IsActive = true
        });

        // Öğrenciyi gruba ve kursa ata (attendance rate hesabı için)
        var groupId = Guid.NewGuid();
        _db.Groups.Add(new Group { Id = groupId, TenantId = _tenantId, Name = "G1" });
        _db.CourseGroups.Add(new CourseGroup { CourseId = _courseId, GroupId = groupId, Mode = CourseMode.Both });
        _db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), UserId = _studentId, GroupId = groupId, Status = "active" });

        _db.SaveChanges();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. RecordJoin — Yeni öğrenci katılımı
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordJoin_NewStudent_ShouldCreateAttendance()
    {
        var result = await _service.RecordJoinAsync(_tenantId, _sessionId, _studentId);

        result.Should().NotBeNull();
        result.UserId.Should().Be(_studentId);
        result.UserFullName.Should().Be("Ali Yılmaz");

        var count = await _db.SessionAttendances.CountAsync();
        count.Should().Be(1);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. RecordJoin — Aynı öğrenci tekrar katılınca duplicate oluşmamalı
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordJoin_ExistingStudent_ShouldNotDuplicate()
    {
        await _service.RecordJoinAsync(_tenantId, _sessionId, _studentId);
        await _service.RecordJoinAsync(_tenantId, _sessionId, _studentId); // İkinci kez

        var count = await _db.SessionAttendances.CountAsync();
        count.Should().Be(1);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. RecordLeave — Süre doğru hesaplanmalı
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordLeave_ShouldCalculateDuration()
    {
        // Katılım kaydını elle oluştur (zamanı 30 dk öncesine ayarla)
        _db.SessionAttendances.Add(new SessionAttendance
        {
            Id = Guid.NewGuid(), SessionId = _sessionId, UserId = _studentId,
            TenantId = _tenantId, JoinedAt = DateTime.UtcNow.AddMinutes(-30)
        });
        await _db.SaveChangesAsync();

        var result = await _service.RecordLeaveAsync(_tenantId, _sessionId, _studentId);

        result.DurationMinutes.Should().NotBeNull();
        result.DurationMinutes.Should().BeGreaterThanOrEqualTo(29); // ±1 dk tolerans
        result.LeftAt.Should().NotBeNull();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. RecordLeave — Katılım kaydı yoksa hata
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordLeave_NoJoinRecord_ShouldThrow()
    {
        Func<Task> act = () => _service.RecordLeaveAsync(_tenantId, _sessionId, _studentId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. GetAttendanceBySession — Oran doğru hesaplanmalı
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttendance_ShouldCalculateRateCorrectly()
    {
        // 1 öğrenci kayıtlı, 1 katıldı (DurationMinutes > 0)
        _db.SessionAttendances.Add(new SessionAttendance
        {
            Id = Guid.NewGuid(), SessionId = _sessionId, UserId = _studentId,
            TenantId = _tenantId, JoinedAt = DateTime.UtcNow.AddMinutes(-60),
            LeftAt = DateTime.UtcNow, DurationMinutes = 60
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetAttendanceBySessionAsync(_tenantId, _sessionId);

        result.TotalAssigned.Should().Be(1);
        result.TotalPresent.Should().Be(1);
        result.AttendanceRate.Should().Be(100);
        result.SessionTitle.Should().Be("Oturum 1");
    }

    // ────────────────────────────────────────────────────────────────────────
    // 6. GetMyHistory — Farklı tenant verisi gelmemeli
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyHistory_ShouldFilterByTenant()
    {
        var otherTenantId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = otherTenantId, Name = "Other", Code = "other", CreatedAt = DateTime.UtcNow });

        // Bu tenant'taki katılım
        _db.SessionAttendances.Add(new SessionAttendance
        {
            Id = Guid.NewGuid(), SessionId = _sessionId, UserId = _studentId,
            TenantId = _tenantId, JoinedAt = DateTime.UtcNow.AddMinutes(-60),
            LeftAt = DateTime.UtcNow, DurationMinutes = 60
        });

        // Diğer tenantaki yapay katılım
        var otherCourseId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();
        _db.Courses.Add(new Course { Id = otherCourseId, TenantId = otherTenantId, Title = "Diğer Ders" });
        _db.Sessions.Add(new Session { Id = otherSessionId, CourseId = otherCourseId, Title = "Diğer Oturum", Status = SessionStatus.Ended });
        _db.SessionAttendances.Add(new SessionAttendance
        {
            Id = Guid.NewGuid(), SessionId = otherSessionId, UserId = _studentId,
            TenantId = otherTenantId, JoinedAt = DateTime.UtcNow.AddMinutes(-30),
            LeftAt = DateTime.UtcNow, DurationMinutes = 30
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetMyAttendanceHistoryAsync(_tenantId, _studentId);

        result.Should().HaveCount(1);
        result[0].SessionTitle.Should().Be("Oturum 1");
    }

    public void Dispose() => _db.Dispose();
}
