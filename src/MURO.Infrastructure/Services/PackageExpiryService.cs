using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Her saatte bir çalışan arka plan servisi.
/// Süresi dolan UserPackage kayıtlarını "expired" yapar,
/// GroupMember erişimlerini kısıtlar ve öğrenciye bildirim gönderir.
/// </summary>
public class PackageExpiryService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PackageExpiryService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public PackageExpiryService(IServiceProvider services, ILogger<PackageExpiryService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PackageExpiryService başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredPackagesAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "PackageExpiryService çalışırken hata oluştu.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Host kapanıyor — normal akış, sessizce çık
                break;
            }
        }
    }

    private async Task ProcessExpiredPackagesAsync()
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MuroDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;

        // Süresi dolmuş ama hâlâ "active" olan paketler
        var expired = await context.UserPackages
            .Where(up => up.Status == "active"
                      && up.ExpiresAt.HasValue
                      && up.ExpiresAt < now)
            .Include(up => up.Package)
                .ThenInclude(p => p.PackageGroups)
            .ToListAsync();

        if (expired.Count == 0) return;

        _logger.LogInformation("{Count} adet süresi dolan paket işleniyor.", expired.Count);

        foreach (var up in expired)
        {
            up.Status = "expired";

            // Bu kullanıcının bu gruptaki GroupMember'larını kontrol et:
            // Başka aktif paketi aynı gruba erişim veriyorsa GroupMember'ı "removed" YAPMA
            foreach (var pg in up.Package.PackageGroups)
            {
                var hasOtherActivePackage = await context.UserPackages
                    .Where(x => x.UserId == up.UserId
                              && x.Id != up.Id
                              && x.Status == "active"
                              && (x.ExpiresAt == null || x.ExpiresAt > now))
                    .Join(context.PackageGroups,
                        x => x.PackageId,
                        pkg => pkg.PackageId,
                        (x, pkg) => pkg.GroupId)
                    .AnyAsync(gId => gId == pg.GroupId);

                if (!hasOtherActivePackage)
                {
                    var gm = await context.GroupMembers
                        .FirstOrDefaultAsync(g => g.UserId == up.UserId && g.GroupId == pg.GroupId);
                    if (gm != null) gm.Status = "removed";
                }
            }

            // Öğrenciye bildirim
            await notifications.CreateAsync(up.Package.TenantId,
                new Application.DTOs.Notifications.CreateNotificationRequest(
                    up.UserId,
                    "Paket Süreniz Doldu",
                    $"\"{up.Package.Name}\" paketinizin süresi doldu. Erişiminiz kısıtlandı.",
                    "PackageExpired",
                    "InApp"));
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("{Count} paket süresi dolduruldu.", expired.Count);
    }
}
