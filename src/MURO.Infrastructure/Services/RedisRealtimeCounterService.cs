using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using StackExchange.Redis;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Redis INCR/DECR ile anlık counter'lar.
/// Online kullanıcı sayısı, aktif ders, bekleyen talep gibi metrikleri tutar.
/// Key format: "muro:{tenantId}:{metricName}"
/// </summary>
public class RedisRealtimeCounterService : IRealtimeCounterService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRealtimeCounterService> _logger;

    public RedisRealtimeCounterService(IConnectionMultiplexer redis, ILogger<RedisRealtimeCounterService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringIncrementAsync($"muro:counter:{key}");
            if (expiry.HasValue)
                await db.KeyExpireAsync($"muro:counter:{key}", expiry.Value);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis counter increment failed: {Key}", key);
            return 0;
        }
    }

    public async Task<long> DecrementAsync(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringDecrementAsync($"muro:counter:{key}");
            // Negatife düşmesin
            if (value < 0) { await db.StringSetAsync($"muro:counter:{key}", 0); return 0; }
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis counter decrement failed: {Key}", key);
            return 0;
        }
    }

    public async Task<long> GetAsync(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync($"muro:counter:{key}");
            return value.HasValue ? (long)value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis counter get failed: {Key}", key);
            return 0;
        }
    }

    public async Task ResetAsync(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync($"muro:counter:{key}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis counter reset failed: {Key}", key);
        }
    }
}
