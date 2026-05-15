using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using StackExchange.Redis;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Redis SETNX ile distributed lock.
/// Lock alındığında IAsyncDisposable döner — using bloğuyla otomatik release.
/// Expiry süresi dolarsa lock otomatik açılır (deadlock koruması).
/// </summary>
public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDistributedLockService> _logger;

    public RedisDistributedLockService(IConnectionMultiplexer redis, ILogger<RedisDistributedLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(string resource, TimeSpan expiry)
    {
        var db = _redis.GetDatabase();
        var lockKey = $"lock:{resource}";
        var lockValue = Guid.NewGuid().ToString();

        var acquired = await db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);

        if (acquired)
        {
            _logger.LogDebug("Lock acquired: {Resource}", resource);
            return new LockHandle(db, lockKey, lockValue, _logger);
        }

        _logger.LogDebug("Lock already held: {Resource}", resource);
        return null;
    }

    private class LockHandle : IAsyncDisposable
    {
        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _value;
        private readonly ILogger _logger;

        public LockHandle(IDatabase db, string key, string value, ILogger logger)
        {
            _db = db;
            _key = key;
            _value = value;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            // Sadece kendi lock'ını sil (başkasının lock'ını silme)
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            try
            {
                await _db.ScriptEvaluateAsync(script,
                    new RedisKey[] { _key },
                    new RedisValue[] { _value });
                _logger.LogDebug("Lock released: {Key}", _key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lock release failed: {Key}", _key);
            }
        }
    }
}
