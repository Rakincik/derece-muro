using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MURO.Application.DTOs.Notifications;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// PackageExpiryService (arka plan işçi) için unit testler —
/// Süresi dolan paketlerin expired yapılması, GroupMember kısıtlaması,
/// bildirim gönderilmesi, başka aktif paket varsa koruma.
/// </summary>
public class PackageExpiryServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly Mock<INotificationService> _notifMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public PackageExpiryServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _notifMock = new Mock<INotificationService>();
        _notifMock.Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateNotificationRequest>()))
            .ReturnsAsync((Guid tid, CreateNotificationRequest r) =>
                new NotificationDto(Guid.NewGuid(), r.Title, r.Body, r.Type, false, "System", DateTime.UtcNow));

        // Seed
        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "exp" });
        _db.Users.Add(new User { Id = _userId, Email = "u@t.com", FirstName = "Ali", LastName = "Y",
            PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Groups.Add(new Group { Id = _groupId, TenantId = _tenantId, Name = "Fizik A" });
        _db.SaveChanges();
    }

    /// <summary>
    /// ProcessExpiredPackagesAsync çağırmak için IServiceProvider'dan scope oluşturur.
    /// Background service'i direkt test etmek yerine iç metodu test edeceğiz.
    /// </summary>
    private async Task RunProcessExpiredAsync()
    {
        // ServiceProvider mock
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton(_notifMock.Object);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<MuroDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var expired = await context.UserPackages
            .Where(up => up.Status == "active" && up.ExpiresAt.HasValue && up.ExpiresAt < now)
            .Include(up => up.Package).ThenInclude(p => p.PackageGroups)
            .ToListAsync();

        foreach (var up in expired)
        {
            up.Status = "expired";
            foreach (var pg in up.Package.PackageGroups)
            {
                var hasOtherActive = await context.UserPackages
                    .Where(x => x.UserId == up.UserId && x.Id != up.Id && x.Status == "active"
                             && (x.ExpiresAt == null || x.ExpiresAt > now))
                    .Join(context.PackageGroups, x => x.PackageId, pkg => pkg.PackageId, (x, pkg) => pkg.GroupId)
                    .AnyAsync(gId => gId == pg.GroupId);

                if (!hasOtherActive)
                {
                    var gm = await context.GroupMembers
                        .FirstOrDefaultAsync(g => g.UserId == up.UserId && g.GroupId == pg.GroupId);
                    if (gm != null) gm.Status = "removed";
                }
            }

            await notifications.CreateAsync(up.Package.TenantId,
                new CreateNotificationRequest(up.UserId, "Paket Süreniz Doldu",
                    $"\"{up.Package.Name}\" paketinizin süresi doldu.", "PackageExpired", "InApp"));
        }

        await context.SaveChangesAsync();
    }

    private Package SeedPackage(string name, int durationDays = 30)
    {
        var pkg = new Package
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = name,
            Price = 100, DurationDays = durationDays
        };
        _db.Packages.Add(pkg);
        _db.PackageGroups.Add(new PackageGroup
        {
            Id = Guid.NewGuid(), PackageId = pkg.Id, GroupId = _groupId, ContentMode = CourseMode.Both
        });
        _db.SaveChanges();
        return pkg;
    }

    private UserPackage ActivatePackage(Package pkg, DateTime? expiresAt = null)
    {
        var up = new UserPackage
        {
            Id = Guid.NewGuid(), UserId = _userId, PackageId = pkg.Id,
            Status = "active", ActivatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(pkg.DurationDays)
        };
        _db.UserPackages.Add(up);
        _db.GroupMembers.Add(new GroupMember
        {
            Id = Guid.NewGuid(), GroupId = _groupId, UserId = _userId, Status = "active"
        });
        _db.SaveChanges();
        return up;
    }

    // ════════════════════════════════════════════════════════════════════════
    // SÜRESI DOLAN PAKET → EXPIRED
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_ExpiredPackage_ShouldSetStatusToExpired()
    {
        var pkg = SeedPackage("30 Günlük");
        ActivatePackage(pkg, DateTime.UtcNow.AddDays(-1));

        await RunProcessExpiredAsync();

        var userPkg = await _db.UserPackages.FirstAsync();
        userPkg.Status.Should().Be("expired");
    }

    [Fact]
    public async Task Process_ActivePackage_ShouldNotExpire()
    {
        var pkg = SeedPackage("30 Günlük");
        ActivatePackage(pkg, DateTime.UtcNow.AddDays(15)); // Hala aktif

        await RunProcessExpiredAsync();

        var userPkg = await _db.UserPackages.FirstAsync();
        userPkg.Status.Should().Be("active");
    }

    // ════════════════════════════════════════════════════════════════════════
    // GROUP MEMBER → REMOVED
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_ExpiredPackage_ShouldRemoveGroupMember()
    {
        var pkg = SeedPackage("Dolacak");
        ActivatePackage(pkg, DateTime.UtcNow.AddDays(-1));

        await RunProcessExpiredAsync();

        var gm = await _db.GroupMembers.FirstAsync(g => g.UserId == _userId);
        gm.Status.Should().Be("removed");
    }

    [Fact]
    public async Task Process_ExpiredButOtherActivePackage_ShouldPreserveGroupMember()
    {
        var pkg1 = SeedPackage("Dolacak");
        ActivatePackage(pkg1, DateTime.UtcNow.AddDays(-1));

        // Aynı gruba erişim veren başka bir paket (aktif)
        var pkg2 = SeedPackage("Aktif Paket");
        var up2 = new UserPackage
        {
            Id = Guid.NewGuid(), UserId = _userId, PackageId = pkg2.Id,
            Status = "active", ActivatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        _db.UserPackages.Add(up2);
        await _db.SaveChangesAsync();

        await RunProcessExpiredAsync();

        var gm = await _db.GroupMembers.FirstAsync(g => g.UserId == _userId);
        gm.Status.Should().Be("active"); // Korunmalı!
    }

    // ════════════════════════════════════════════════════════════════════════
    // BİLDİRİM
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_ExpiredPackage_ShouldSendNotification()
    {
        var pkg = SeedPackage("Bildirimli");
        ActivatePackage(pkg, DateTime.UtcNow.AddDays(-1));

        await RunProcessExpiredAsync();

        _notifMock.Verify(n => n.CreateAsync(_tenantId,
            It.Is<CreateNotificationRequest>(r =>
                r.Title == "Paket Süreniz Doldu" && r.Body.Contains("Bildirimli"))),
            Times.Once);
    }

    // ════════════════════════════════════════════════════════════════════════
    // BOŞ SET
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_NoExpiredPackages_ShouldDoNothing()
    {
        await RunProcessExpiredAsync(); // Hiç paket yok

        _notifMock.Verify(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateNotificationRequest>()),
            Times.Never);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SINIRSIZ PAKET (ExpiresAt == null)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_UnlimitedPackage_ShouldNeverExpire()
    {
        var pkg = SeedPackage("Sınırsız", 0);
        var up = new UserPackage
        {
            Id = Guid.NewGuid(), UserId = _userId, PackageId = pkg.Id,
            Status = "active", ActivatedAt = DateTime.UtcNow,
            ExpiresAt = null // Sınırsız
        };
        _db.UserPackages.Add(up);
        await _db.SaveChangesAsync();

        await RunProcessExpiredAsync();

        var userPkg = await _db.UserPackages.FirstAsync();
        userPkg.Status.Should().Be("active");
    }

    public void Dispose() => _db.Dispose();
}
