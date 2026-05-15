using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Notifications;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

public class PackageServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly PackageService _service;
    private readonly Mock<INotificationService> _notifMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _user1 = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public PackageServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new MuroDbContext(options);
        _notifMock = new Mock<INotificationService>();
        _notifMock.Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateNotificationRequest>()))
            .ReturnsAsync((Guid tid, CreateNotificationRequest r) =>
                new NotificationDto(Guid.NewGuid(), r.Title, r.Body, r.Type, false, "System", DateTime.UtcNow));
        _service = new PackageService(_db, _notifMock.Object, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "T", Code = "pkg", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User { Id = _user1, Email = "u1@t.com", FirstName = "Ali", LastName = "Y", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Groups.Add(new Group { Id = _groupId, TenantId = _tenantId, Name = "Fizik Sınıfı" });
        _db.SaveChanges();
    }

    private CreatePackageRequest Pkg(string name, int dur = 30, bool withGroup = false) =>
        new(name, "Test", 100m, dur,
            withGroup ? new List<PackageGroupRequest> { new(_groupId, "Both") } : new List<PackageGroupRequest>());

    [Fact]
    public async Task CreatePackage_ShouldSaveWithGroups()
    {
        var r = await _service.CreatePackageAsync(_tenantId, new CreatePackageRequest(
            "Premium", "Tüm dersler", 999.99m, 365,
            new List<PackageGroupRequest> { new(_groupId, "Both") }));
        r.Name.Should().Be("Premium");
        r.Price.Should().Be(999.99m);
        r.Groups.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPackages_ShouldReturnAll()
    {
        await _service.CreatePackageAsync(_tenantId, Pkg("A"));
        await _service.CreatePackageAsync(_tenantId, Pkg("B"));
        (await _service.GetPackagesAsync(_tenantId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPackageById_ShouldReturn()
    {
        var c = await _service.CreatePackageAsync(_tenantId, Pkg("D"));
        (await _service.GetPackageByIdAsync(_tenantId, c.Id)).Name.Should().Be("D");
    }

    [Fact]
    public async Task GetPackageById_NotFound_ShouldThrow()
    {
        var act = () => _service.GetPackageByIdAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdatePackage_ShouldModifyFields()
    {
        var p = await _service.CreatePackageAsync(_tenantId, Pkg("Eski"));
        var u = await _service.UpdatePackageAsync(_tenantId, p.Id, new UpdatePackageRequest("Yeni", null, 1500m, 180, null));
        u.Name.Should().Be("Yeni");
        u.Price.Should().Be(1500m);
    }

    [Fact]
    public async Task DeletePackage_ShouldRemove()
    {
        var p = await _service.CreatePackageAsync(_tenantId, Pkg("Sil"));
        await _service.DeletePackageAsync(_tenantId, p.Id);
        (await _db.Packages.AnyAsync(x => x.Id == p.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ActivatePackage_WithDuration_ShouldSetExpiry()
    {
        var p = await _service.CreatePackageAsync(_tenantId, Pkg("30g", 30));
        var r = await _service.ActivateUserPackageAsync(_user1, p.Id);
        r.Status.Should().Be("active");
        r.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ActivatePackage_Unlimited_ShouldHaveNoExpiry()
    {
        var p = await _service.CreatePackageAsync(_tenantId, Pkg("Sınırsız", 0));
        var r = await _service.ActivateUserPackageAsync(_user1, p.Id);
        r.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task ActivatePackage_ShouldAddToPackageGroups()
    {
        var p = await _service.CreatePackageAsync(_tenantId, Pkg("Gruplu", withGroup: true));
        await _service.ActivateUserPackageAsync(_user1, p.Id);
        (await _db.GroupMembers.AnyAsync(gm => gm.UserId == _user1 && gm.GroupId == _groupId && gm.Status == "active"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ActivatePackage_ShouldSendNotification()
    {
        var p = await _service.CreatePackageAsync(_tenantId, Pkg("Bildirimli"));
        await _service.ActivateUserPackageAsync(_user1, p.Id);
        _notifMock.Verify(n => n.CreateAsync(_tenantId,
            It.Is<CreateNotificationRequest>(r => r.Title == "Paketiniz Aktive Edildi" && r.Body.Contains("Bildirimli"))), Times.Once);
    }

    [Fact]
    public async Task ActivatePackage_ManualExpiry_ShouldOverride()
    {
        var p = await _service.CreatePackageAsync(_tenantId, Pkg("Override", 30));
        var custom = DateTime.UtcNow.AddDays(90);
        var r = await _service.ActivateUserPackageAsync(_user1, p.Id, manualExpiresAt: custom);
        r.ExpiresAt.Should().BeCloseTo(custom, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExtendPackage_ShouldAddDays()
    {
        var p = await _service.CreatePackageAsync(_tenantId, Pkg("Uzat", 30));
        var up = await _service.ActivateUserPackageAsync(_user1, p.Id);
        var orig = up.ExpiresAt!.Value;
        var ext = await _service.ExtendPackageAsync(up.Id, 15);
        ext.ExpiresAt.Should().BeCloseTo(orig.AddDays(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExtendPackage_Expired_ShouldStartFromNow()
    {
        var p = await _service.CreatePackageAsync(_tenantId, Pkg("Dolmuş", 1));
        var up = await _service.ActivateUserPackageAsync(_user1, p.Id);
        var userPkg = await _db.UserPackages.FindAsync(up.Id);
        userPkg!.ExpiresAt = DateTime.UtcNow.AddDays(-10);
        await _db.SaveChangesAsync();
        var ext = await _service.ExtendPackageAsync(up.Id, 30);
        ext.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CancelPackage_ShouldSetCancelled()
    {
        var p = await _service.CreatePackageAsync(_tenantId, Pkg("İptal"));
        var up = await _service.ActivateUserPackageAsync(_user1, p.Id);
        await _service.CancelPackageAsync(up.Id);
        var dbUp = await _db.UserPackages.FindAsync(up.Id);
        dbUp!.Status.Should().Be("cancelled");
        _notifMock.Verify(n => n.CreateAsync(_tenantId,
            It.Is<CreateNotificationRequest>(r => r.Title == "Paket İptal Edildi")), Times.Once);
    }

    [Fact]
    public async Task GetUserPackages_ShouldReturnHistory()
    {
        var p1 = await _service.CreatePackageAsync(_tenantId, Pkg("P1", 30));
        var p2 = await _service.CreatePackageAsync(_tenantId, Pkg("P2", 90));
        await _service.ActivateUserPackageAsync(_user1, p1.Id);
        await _service.ActivateUserPackageAsync(_user1, p2.Id);
        var h = await _service.GetUserPackagesAsync(_user1);
        h.Should().HaveCount(2);
        h[0].PackageName.Should().Be("P2");
    }

    public void Dispose() => _db.Dispose();
}
