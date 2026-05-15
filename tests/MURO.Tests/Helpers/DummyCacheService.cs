using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MURO.Application.Interfaces;

namespace MURO.Tests.Helpers;

public class DummyCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, object> _cache = new();

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue(key, out var cachedValue))
        {
            return (T)cachedValue;
        }

        var value = await factory();
        _cache[key] = value!;
        return value;
    }

    public Task RemoveAsync(string key)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        foreach (var key in _cache.Keys)
        {
            if (key.StartsWith(prefix))
            {
                _cache.TryRemove(key, out _);
            }
        }
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        if (_cache.TryGetValue(key, out var val)) return Task.FromResult((T?)val);
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        _cache[key] = value!;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(_cache.ContainsKey(key));
    }

    public Task<CacheStats> GetCacheStatsAsync()
    {
        return Task.FromResult(new CacheStats(_cache.Count, "0", "0", 0, 0, 0, "0", "Dummy"));
    }
}
