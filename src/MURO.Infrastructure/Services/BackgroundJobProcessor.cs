using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Arka plan job işleyici — kuyruktan job alır ve tipine göre ilgili servisi çağırır.
/// API process'inde HostedService olarak çalışır.
/// </summary>
public sealed class BackgroundJobProcessor : BackgroundService
{
    private readonly IBackgroundJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobProcessor> _logger;

    public BackgroundJobProcessor(
        IBackgroundJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundJobProcessor> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundJobProcessor başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            BackgroundJob job;
            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // Graceful shutdown
            }

            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job işlenirken hata: {JobType}", job.GetType().Name);
            }
        }

        _logger.LogInformation("BackgroundJobProcessor durduruldu.");
    }

    private async Task ProcessJobAsync(BackgroundJob job, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        switch (job)
        {
            case AuditLogJob audit:
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                await auditService.LogAsync(
                    audit.TenantId, audit.UserId, audit.UserName,
                    audit.Action, audit.EntityType, audit.EntityId,
                    audit.EntityName, audit.Details, audit.IpAddress);
                break;

            case CacheInvalidationJob cache:
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
                await cacheService.RemoveByPrefixAsync(cache.Prefix);
                break;

            case BatchCacheInvalidationJob batch:
                var batchCache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                foreach (var prefix in batch.Prefixes)
                    await batchCache.RemoveByPrefixAsync(prefix);
                break;

            default:
                _logger.LogWarning("Bilinmeyen job tipi: {Type}", job.GetType().Name);
                break;
        }
    }
}
