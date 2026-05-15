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
/// StudentService için unit testler — Öğrenci takvim etkinlikleri,
/// session + calendar event birleşimi, grup filtresi, sıralama.
/// </summary>
public class StudentServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly StudentService _service;
    private readonly Mock<IGroupAccessService> _groupAccessMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public StudentServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _groupAccessMock = new Mock<IGroupAccessService>();
        _service = new StudentService(_db, _groupAccessMock.Object, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "stu" });
        _db.Users.Add(new User { Id = _userId, Email = "s@t.com", FirstName = "Ali", LastName = "Y", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Fizik 101", IsPublished = true });
        _db.Groups.Add(new Group { Id = _groupId, TenantId = _tenantId, Name = "Fizik A" });
        _db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = _groupId, UserId = _userId, Status = "active" });
        _db.SaveChanges();
    }

    private void SetAccessibleCourses(params Guid[] courseIds)
    {
        _groupAccessMock.Setup(g => g.GetAccessibleCourseIdsAsync(_tenantId, _userId))
            .ReturnsAsync(new HashSet<Guid>(courseIds));
    }

    // ════════════════════════════════════════════════════════════════════════
    // SESSION EVENTS (Canlı ders takvimi)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCalendarEvents_Sessions_ShouldInclude()
    {
        SetAccessibleCourses(_courseId);
        _db.Sessions.Add(new Session
        {
            Id = Guid.NewGuid(), CourseId = _courseId, Title = "Canlı Ders 1",
            ScheduledStart = DateTime.UtcNow.AddDays(1), ScheduledEnd = DateTime.UtcNow.AddDays(1).AddHours(1)
        });
        await _db.SaveChangesAsync();

        var events = await _service.GetCalendarEventsAsync(_tenantId, _userId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        events.Should().HaveCountGreaterThanOrEqualTo(1);
        events.Should().Contain(e => e.Title == "Canlı Ders 1");
    }

    [Fact]
    public async Task GetCalendarEvents_Sessions_ShouldOnlyShowAccessibleCourses()
    {
        var otherCourseId = Guid.NewGuid();
        _db.Courses.Add(new Course { Id = otherCourseId, TenantId = _tenantId, Title = "Gizli Kurs", IsPublished = true });
        _db.Sessions.Add(new Session { Id = Guid.NewGuid(), CourseId = _courseId, Title = "Erişilebilir", ScheduledStart = DateTime.UtcNow.AddDays(1) });
        _db.Sessions.Add(new Session { Id = Guid.NewGuid(), CourseId = otherCourseId, Title = "Erişilemez", ScheduledStart = DateTime.UtcNow.AddDays(1) });
        await _db.SaveChangesAsync();

        SetAccessibleCourses(_courseId); // Sadece Fizik'e erişim

        var events = await _service.GetCalendarEventsAsync(_tenantId, _userId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        events.Should().Contain(e => e.Title == "Erişilebilir");
        events.Should().NotContain(e => e.Title == "Erişilemez");
    }

    // ════════════════════════════════════════════════════════════════════════
    // CALENDAR EVENTS (Genel + Grup)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCalendarEvents_TenantWide_ShouldInclude()
    {
        SetAccessibleCourses(_courseId);
        _db.CalendarEvents.Add(new CalendarEvent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Title = "Genel Etkinlik",
            EventType = "Duyuru", StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(1).AddHours(2),
            GroupId = null // Tenant-wide → herkes görmeli
        });
        await _db.SaveChangesAsync();

        var events = await _service.GetCalendarEventsAsync(_tenantId, _userId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        events.Should().Contain(e => e.Title == "Genel Etkinlik");
    }

    [Fact]
    public async Task GetCalendarEvents_GroupEvent_ShouldShowOnlyMyGroupEvents()
    {
        SetAccessibleCourses(_courseId);
        var otherGroupId = Guid.NewGuid();
        _db.Groups.Add(new Group { Id = otherGroupId, TenantId = _tenantId, Name = "Mat B" });

        _db.CalendarEvents.Add(new CalendarEvent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Title = "Fizik Etkinlik",
            EventType = "Ders", StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(1).AddHours(1), GroupId = _groupId
        });
        _db.CalendarEvents.Add(new CalendarEvent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Title = "Mat Etkinlik",
            EventType = "Ders", StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(1).AddHours(1), GroupId = otherGroupId
        });
        await _db.SaveChangesAsync();

        var events = await _service.GetCalendarEventsAsync(_tenantId, _userId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        events.Should().Contain(e => e.Title == "Fizik Etkinlik");
        events.Should().NotContain(e => e.Title == "Mat Etkinlik"); // Farklı grubun etkinliği
    }

    // ════════════════════════════════════════════════════════════════════════
    // DATE RANGE FILTER
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCalendarEvents_DateRange_ShouldFilter()
    {
        SetAccessibleCourses(_courseId);
        _db.CalendarEvents.Add(new CalendarEvent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Title = "Bu hafta",
            EventType = "Ders", StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(1).AddHours(1)
        });
        _db.CalendarEvents.Add(new CalendarEvent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Title = "Gelecek ay",
            EventType = "Ders", StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(30).AddHours(1)
        });
        await _db.SaveChangesAsync();

        var events = await _service.GetCalendarEventsAsync(_tenantId, _userId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        events.Should().Contain(e => e.Title == "Bu hafta");
        events.Should().NotContain(e => e.Title == "Gelecek ay");
    }

    // ════════════════════════════════════════════════════════════════════════
    // MERGED & SORTED
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCalendarEvents_ShouldMergeAndSortByStartDate()
    {
        SetAccessibleCourses(_courseId);
        _db.Sessions.Add(new Session
        {
            Id = Guid.NewGuid(), CourseId = _courseId, Title = "Canlı Ders",
            ScheduledStart = DateTime.UtcNow.AddDays(3)
        });
        _db.CalendarEvents.Add(new CalendarEvent
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Title = "Duyuru",
            EventType = "Duyuru", StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(1).AddHours(1)
        });
        await _db.SaveChangesAsync();

        var events = await _service.GetCalendarEventsAsync(_tenantId, _userId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        events.Should().HaveCountGreaterThanOrEqualTo(2);
        events[0].Title.Should().Be("Duyuru"); // Daha erken tarihli
    }

    [Fact]
    public async Task GetCalendarEvents_EmptyResult_ShouldReturnEmptyList()
    {
        SetAccessibleCourses(_courseId);
        var events = await _service.GetCalendarEventsAsync(_tenantId, _userId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        events.Should().BeEmpty();
    }

    public void Dispose() => _db.Dispose();
}
