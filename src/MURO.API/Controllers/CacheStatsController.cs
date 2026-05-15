using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

/// <summary>
/// Cache istatistikleri — Redis monitoring endpoint.
/// Sadece admin erişimi.
/// </summary>
[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/admin/cache")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class CacheStatsController : ControllerBase
{
    private readonly ICacheService _cache;

    public CacheStatsController(ICacheService cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// GET /api/v1/admin/cache/stats
    /// Redis key sayısı, memory kullanımı, HIT/MISS oranları
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _cache.GetCacheStatsAsync();
        return Ok(new
        {
            status = "ok",
            redis = new
            {
                totalKeys = stats.TotalKeys,
                memoryUsed = stats.MemoryUsed,
                memoryPeak = stats.MemoryPeak,
                hitCount = stats.HitCount,
                missCount = stats.MissCount,
                hitRate = $"{stats.HitRate}%",
                uptime = stats.Uptime,
                version = stats.RedisVersion
            }
        });
    }

    /// <summary>
    /// DELETE /api/v1/admin/cache/flush?prefix=tenantId:
    /// Belirli prefix'e sahip cache key'lerini temizle.
    /// </summary>
    [HttpDelete("flush")]
    public async Task<IActionResult> FlushByPrefix([FromQuery] string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return BadRequest(new { error = "prefix parametresi gerekli." });

        await _cache.RemoveByPrefixAsync(prefix);
        return Ok(new { status = "flushed", prefix });
    }
}
