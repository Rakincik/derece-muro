using System.Threading.Channels;

namespace MURO.Application.Interfaces;

/// <summary>
/// In-process background job queue — Channel&lt;T&gt; tabanlı.
/// Fire-and-forget işlemler (audit log, cache invalidation, bildirim) için kullanılır.
/// </summary>
public interface IBackgroundJobQueue
{
    /// <summary>Job'u kuyruğa ekle (non-blocking, ~0.1ms).</summary>
    ValueTask EnqueueAsync(BackgroundJob job);

    /// <summary>Kuyruktan sonraki job'u al (blocking, consumer tarafı).</summary>
    ValueTask<BackgroundJob> DequeueAsync(CancellationToken ct);
}

// ─── Job Tipleri ──────────────────────────────────────────────────────────────

/// <summary>Tüm job tiplerinin base sınıfı.</summary>
public abstract record BackgroundJob;

/// <summary>Audit log kaydı — controller'dan fire-and-forget olarak gönderilir.</summary>
public record AuditLogJob(
    Guid? TenantId,
    Guid? UserId,
    string? UserName,
    string Action,
    string EntityType,
    string? EntityId,
    string? EntityName,
    string? Details,
    string? IpAddress
) : BackgroundJob;

/// <summary>Cache prefix invalidation — CRUD sonrası arka planda çalışır.</summary>
public record CacheInvalidationJob(string Prefix) : BackgroundJob;

/// <summary>Birden fazla cache prefix'ini aynı anda temizler.</summary>
public record BatchCacheInvalidationJob(string[] Prefixes) : BackgroundJob;
