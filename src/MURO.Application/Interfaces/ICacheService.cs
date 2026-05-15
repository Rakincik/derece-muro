namespace MURO.Application.Interfaces;

/// <summary>
/// Distributed cache abstraction — Redis ile backend cache yönetimi.
/// Multi-tenant uyumlu: key prefix olarak tenantId kullanılır.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Cache'te varsa döndür, yoksa factory çalıştır, sonucu cache'le ve döndür.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);

    /// <summary>
    /// Cache'ten direkt okuma (factory olmadan). Key yoksa default döner.
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Cache'e direkt yazma.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);

    /// <summary>
    /// Key var mı kontrolü.
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Tekil key invalidation.
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// Pattern-based invalidation (örn: "tenant-001:courses:*").
    /// CRUD işlemlerinden sonra ilgili cache'i temizlemek için.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix);

    /// <summary>
    /// Cache istatistikleri: key sayısı, memory kullanımı.
    /// </summary>
    Task<CacheStats> GetCacheStatsAsync();
}

/// <summary>
/// Cache monitoring istatistikleri.
/// </summary>
public record CacheStats(
    long TotalKeys,
    string MemoryUsed,
    string MemoryPeak,
    long HitCount,
    long MissCount,
    double HitRate,
    string Uptime,
    string RedisVersion);
