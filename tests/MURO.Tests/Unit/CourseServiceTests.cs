using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Courses;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Moq;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

public class CourseServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly CourseSessionService _courseService;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CourseServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        
        var bbbMock = new Mock<IBbbService>();
        var notifMock = new Mock<INotificationService>();
        var grpAccessMock = new Mock<IGroupAccessService>();
        var cfgMock = new Mock<IConfiguration>();

        ICacheService cacheMock = new MURO.Tests.Helpers.DummyCacheService();
        _courseService = new CourseSessionService(_db, grpAccessMock.Object, cacheMock);

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant", Code = "test", CreatedAt = DateTime.UtcNow });
        _db.SaveChanges();
    }

    private async Task<(Guid courseId, Guid groupId)> SetupCourseAndGroupAsync()
    {
        var groupId = Guid.NewGuid();
        _db.Groups.Add(new Group { Id = groupId, TenantId = _tenantId, Name = "Test Group" });

        var courseId = Guid.NewGuid();
        _db.Courses.Add(new Course { Id = courseId, TenantId = _tenantId, Title = "Conflict Test Course" });
        
        _db.CourseGroups.Add(new CourseGroup { CourseId = courseId, GroupId = groupId, Mode = CourseMode.Both });
        await _db.SaveChangesAsync();

        return (courseId, groupId);
    }

    [Fact]
    public async Task CreateSession_WithOverlappingTimeInSameGroup_ShouldThrowInvalidOperationException()
    {
        var (courseId1, groupId) = await SetupCourseAndGroupAsync();
        
        var courseId2 = Guid.NewGuid();
        _db.Courses.Add(new Course { Id = courseId2, TenantId = _tenantId, Title = "Other Course" });
        _db.CourseGroups.Add(new CourseGroup { CourseId = courseId2, GroupId = groupId, Mode = CourseMode.Both });
        await _db.SaveChangesAsync();

        var start = DateTime.UtcNow.AddDays(1);

        await _courseService.CreateSessionAsync(_tenantId, courseId1, new CreateSessionRequest(
            "Session 1", null, null, null, 60, false, start, start.AddMinutes(60), true
        ));

        var req2 = new CreateSessionRequest("Session 2", null, null, null, 60, false, start, start.AddMinutes(60), true);
        
        Func<Task> act = async () => await _courseService.CreateSessionAsync(_tenantId, courseId1, req2);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*çakışma*");
    }

    [Fact]
    public async Task CreateSession_WithNonOverlappingTime_ShouldSucceed()
    {
        var (courseId1, groupId) = await SetupCourseAndGroupAsync();
        var start = DateTime.UtcNow.AddDays(1);

        await _courseService.CreateSessionAsync(_tenantId, courseId1, new CreateSessionRequest(
            "Session 1", null, null, null, 60, false, start, start.AddMinutes(60), true
        ));

        var req2 = new CreateSessionRequest("Session 2", null, null, null, 60, false, start.AddHours(2), start.AddHours(3), true);
        
        var result = await _courseService.CreateSessionAsync(_tenantId, courseId1, req2);
        
        result.Should().NotBeNull();
        result.Title.Should().Be("Session 2");
    }

    [Fact]
    public async Task CreateSession_WithOverlappingTimeInDifferentGroup_ShouldSucceed()
    {
        var (courseId1, groupId1) = await SetupCourseAndGroupAsync();
        
        var groupId2 = Guid.NewGuid();
        _db.Groups.Add(new Group { Id = groupId2, TenantId = _tenantId, Name = "Independent Group" });
        var courseId2 = Guid.NewGuid();
        _db.Courses.Add(new Course { Id = courseId2, TenantId = _tenantId, Title = "Independent Course" });
        _db.CourseGroups.Add(new CourseGroup { CourseId = courseId2, GroupId = groupId2, Mode = CourseMode.Both });
        await _db.SaveChangesAsync();

        var start = DateTime.UtcNow.AddDays(1);

        await _courseService.CreateSessionAsync(_tenantId, courseId1, new CreateSessionRequest(
            "Session 1", null, null, null, 60, false, start, start.AddMinutes(60), true
        ));

        var req2 = new CreateSessionRequest("Session 2", null, null, null, 60, false, start, start.AddMinutes(60), true);
        var result = await _courseService.CreateSessionAsync(_tenantId, courseId2, req2);

        result.Should().NotBeNull();
        result.Title.Should().Be("Session 2");
    }

    public void Dispose() => _db.Dispose();
}
