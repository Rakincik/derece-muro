using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using StackExchange.Redis;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Redis-backed distributed cache.
/// - GetOrSetAsync: cache-aside pattern (cache varsa dön, yoksa factory + cache)
/// - RemoveByPrefixAsync: SCAN ile pattern-based invalidation
/// - Tüm serialization System.Text.Json ile (performans)
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    public RedisCacheService(
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _redis = redis;
        _logger = logger;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        try
        {
            // 1. Cache'ten oku
            var cached = await _cache.GetStringAsync(key);
            if (cached != null)
            {
                _logger.LogDebug("Cache HIT: {Key}", key);
                return JsonSerializer.Deserialize<T>(cached)!;
            }
        }
        catch (Exception ex)
        {
            // Redis bağlantı hatası — cache'siz devam et, DB'ye düş
            _logger.LogWarning(ex, "Redis cache read failed for key: {Key}", key);
        }

        // 2. Cache MISS — factory'den veri al
        _logger.LogDebug("Cache MISS: {Key}", key);
        var result = await factory();

        try
        {
            // 3. Sonucu cache'e yaz
            var json = JsonSerializer.Serialize(result);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
            };
            await _cache.SetStringAsync(key, json, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache write failed for key: {Key}", key);
        }

        return result;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var cached = await _cache.GetStringAsync(key);
            if (cached != null)
            {
                _logger.LogDebug("Cache GET HIT: {Key}", key);
                return JsonSerializer.Deserialize<T>(cached);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache get failed for key: {Key}", key);
        }
        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
            };
            await _cache.SetStringAsync(key, json, options);
            _logger.LogDebug("Cache SET: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache set failed for key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache exists check failed for key: {Key}", key);
            return false;
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
            _logger.LogDebug("Cache REMOVED: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache remove failed for key: {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            // 🐛 FIX: IDistributedCache adds "muro:" prefix. We must include it in raw Keys query.
            var keys = server.Keys(pattern: $"muro:{prefix}*").ToArray();

            if (keys.Length > 0)
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(keys);
                _logger.LogDebug("Cache REMOVED by prefix: {Prefix} ({Count} keys)", prefix, keys.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache prefix remove failed: {Prefix}", prefix);
        }
    }

    public async Task<CacheStats> GetCacheStatsAsync()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var info = await server.InfoAsync();

            long totalKeys = 0;
            string memoryUsed = "N/A", memoryPeak = "N/A", uptime = "N/A", version = "N/A";
            long hits = 0, misses = 0;

            foreach (var section in info)
            {
                foreach (var kv in section)
                {
                    switch (kv.Key)
                    {
                        case "used_memory_human": memoryUsed = kv.Value; break;
                        case "used_memory_peak_human": memoryPeak = kv.Value; break;
                        case "keyspace_hits": long.TryParse(kv.Value, out hits); break;
                        case "keyspace_misses": long.TryParse(kv.Value, out misses); break;
                        case "uptime_in_seconds":
                            if (long.TryParse(kv.Value, out var secs))
                                uptime = TimeSpan.FromSeconds(secs).ToString(@"dd\.hh\:mm\:ss");
                            break;
                        case "redis_version": version = kv.Value; break;
                    }
                }
            }

            // Total keys — tüm keyleri say
            totalKeys = server.Keys(pattern: "*").Count();
            var hitRate = hits + misses > 0 ? Math.Round((double)hits / (hits + misses) * 100, 2) : 0;

            return new CacheStats(totalKeys, memoryUsed, memoryPeak, hits, misses, hitRate, uptime, version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache stats failed");
            return new CacheStats(0, "N/A", "N/A", 0, 0, 0, "N/A", "N/A");
        }
    }
}

