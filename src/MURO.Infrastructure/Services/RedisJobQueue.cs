using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using StackExchange.Redis;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Redis List (LPUSH/BRPOP) ile güvenilir job queue.
/// İşler JSON payload olarak kuyruğa eklenir, worker sırayla alır.
/// </summary>
public class RedisJobQueue : IJobQueue
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisJobQueue> _logger;

    public RedisJobQueue(IConnectionMultiplexer redis, ILogger<RedisJobQueue> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task EnqueueAsync(string queueName, string payload)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.ListLeftPushAsync($"muro:queue:{queueName}", payload);
            _logger.LogDebug("Job enqueued: {Queue} ({Length} bytes)", queueName, payload.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job enqueue failed: {Queue}", queueName);
            throw; // Job kaybını önle — çağıran bilmeli
        }
    }

    public async Task<string?> DequeueAsync(string queueName, TimeSpan? timeout = null)
    {
        try
        {
            var db = _redis.GetDatabase();
            // RPOP ile FIFO sırasında al
            var value = await db.ListRightPopAsync($"muro:queue:{queueName}");
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Job dequeue failed: {Queue}", queueName);
            return null;
        }
    }

    public async Task<long> GetQueueLengthAsync(string queueName)
    {
        try
        {
            var db = _redis.GetDatabase();
            return await db.ListLengthAsync($"muro:queue:{queueName}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Queue length check failed: {Queue}", queueName);
            return 0;
        }
    }
}
