using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// GroupAccessService için unit testler — Tenant izolasyonu ve erişim kontrolü.
/// </summary>
public class GroupAccessServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly GroupAccessService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();

    public GroupAccessServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _service = new GroupAccessService(_db);

        // İki ayrı tenant oluştur
        _db.Tenants.AddRange(
            new Tenant { Id = _tenantId, Name = "Tenant A", Code = "a", CreatedAt = DateTime.UtcNow },
            new Tenant { Id = _otherTenantId, Name = "Tenant B", Code = "b", CreatedAt = DateTime.UtcNow }
        );
        _db.SaveChanges();
    }

    private User CreateUser(UserRole role = UserRole.Student)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = $"user_{Guid.NewGuid():N}@test.com",
            FirstName = "Test", LastName = "User",
            PasswordHash = "hash", Role = role, IsActive = true
        };
        _db.Users.Add(user);
        return user;
    }

    private (Guid groupId, Guid courseId) CreateGroupWithCourse(Guid tenantId, CourseMode mode = CourseMode.Both)
    {
        var groupId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _db.Groups.Add(new Group { Id = groupId, TenantId = tenantId, Name = "G" });
        _db.Courses.Add(new Course { Id = courseId, TenantId = tenantId, Title = "C", IsPublished = true });
        _db.CourseGroups.Add(new CourseGroup { CourseId = courseId, GroupId = groupId, Mode = mode });
        return (groupId, courseId);
    }

    private void ActivatePackageForUser(Guid userId, Guid groupId, CourseMode contentMode = CourseMode.Both, DateTime? expiresAt = null)
    {
        var pkgId = Guid.NewGuid();
        _db.Packages.Add(new Package { Id = pkgId, TenantId = _tenantId, Name = "Pkg", Price = 100 });
        _db.PackageGroups.Add(new PackageGroup { Id = Guid.NewGuid(), PackageId = pkgId, GroupId = groupId, ContentMode = contentMode });
        _db.UserPackages.Add(new UserPackage
        {
            Id = Guid.NewGuid(), UserId = userId, PackageId = pkgId,
            Status = "active", ExpiresAt = expiresAt
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. Staff (Admin/Instructor) tüm kurslara erişebilmeli
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Staff_ShouldAccessAllCourses()
    {
        var admin = CreateUser(UserRole.Admin);
        var (_, courseId1) = CreateGroupWithCourse(_tenantId);
        var (_, courseId2) = CreateGroupWithCourse(_tenantId);
        await _db.SaveChangesAsync();

        var ids = await _service.GetAccessibleCourseIdsAsync(_tenantId, admin.Id);

        ids.Should().Contain(courseId1);
        ids.Should().Contain(courseId2);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. Aktif Paket → Grup kurslarına erişim
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Student_WithActivePackage_ShouldAccessGroupCourses()
    {
        var student = CreateUser();
        var (groupId, courseId) = CreateGroupWithCourse(_tenantId);
        ActivatePackageForUser(student.Id, groupId);
        await _db.SaveChangesAsync();

        var ids = await _service.GetAccessibleCourseIdsAsync(_tenantId, student.Id);

        ids.Should().Contain(courseId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. Süresi dolmuş paket → erişim yok
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Student_ExpiredPackage_ShouldNotAccess()
    {
        var student = CreateUser();
        var (groupId, courseId) = CreateGroupWithCourse(_tenantId);
        ActivatePackageForUser(student.Id, groupId, expiresAt: DateTime.UtcNow.AddDays(-1)); // süresi dolmuş
        await _db.SaveChangesAsync();

        var ids = await _service.GetAccessibleCourseIdsAsync(_tenantId, student.Id);

        ids.Should().NotContain(courseId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. Direkt grup üyeliği → erişim var
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Student_DirectGroupMember_ShouldAccess()
    {
        var student = CreateUser();
        var (groupId, courseId) = CreateGroupWithCourse(_tenantId);
        _db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), UserId = student.Id, GroupId = groupId, Status = "active" });
        await _db.SaveChangesAsync();

        var ids = await _service.GetAccessibleCourseIdsAsync(_tenantId, student.Id);

        ids.Should().Contain(courseId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. Hiç atama yoksa → erişim yok
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Student_NoPackageNoGroup_ShouldNotAccess()
    {
        var student = CreateUser();
        var (_, courseId) = CreateGroupWithCourse(_tenantId);
        await _db.SaveChangesAsync();

        var ids = await _service.GetAccessibleCourseIdsAsync(_tenantId, student.Id);

        ids.Should().BeEmpty();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 6. Offline paket → Canlı derse erişim yok
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Student_OfflinePackage_ShouldNotAccessLive()
    {
        var student = CreateUser();
        var (groupId, courseId) = CreateGroupWithCourse(_tenantId, CourseMode.Both);
        ActivatePackageForUser(student.Id, groupId, contentMode: CourseMode.Offline);
        await _db.SaveChangesAsync();

        var liveIds = await _service.GetLiveAccessibleCourseIdsAsync(_tenantId, student.Id);

        liveIds.Should().NotContain(courseId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 7. Farklı tenant kursuna erişim yok (TENANT İZOLASYONU)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Student_CrossTenant_ShouldNotAccess()
    {
        var student = CreateUser();
        // Kurs diğer tenant'ta
        var (groupId, courseId) = CreateGroupWithCourse(_otherTenantId);
        // Paket bu tenant'ta aktif ama kurs diğer tenant'ta
        ActivatePackageForUser(student.Id, groupId);
        await _db.SaveChangesAsync();

        var ids = await _service.GetAccessibleCourseIdsAsync(_tenantId, student.Id);

        ids.Should().NotContain(courseId);
    }

    public void Dispose() => _db.Dispose();
}
