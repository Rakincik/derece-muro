using Microsoft.EntityFrameworkCore;
using MURO.Infrastructure.Persistence;

namespace MURO.API.Middleware;

/// <summary>
/// Şüpheli IP'leri otomatik engelleyen middleware.
/// Son 1 saatte 3+ BRUTE_FORCE_DETECTED veya 10+ LOGIN_FAILED olan IP'leri bloklar.
/// Blok süresi: 1 saat (cache'te tutulur).
/// </summary>
public class IpBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpBlacklistMiddleware> _logger;

    // Memory cache: IP → block expire time
    private static readonly Dictionary<string, DateTime> _blockedIps = new();
    private static readonly object _lock = new();
    private static DateTime _lastCleanup = DateTime.UtcNow;

    private const int BruteForceThreshold = 3;
    private const int LoginFailThreshold = 10;
    private static readonly TimeSpan CheckWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan BlockDuration = TimeSpan.FromHours(1);

    public IpBlacklistMiddleware(RequestDelegate next, ILogger<IpBlacklistMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, MuroDbContext db)
    {
        var clientIp = GetClientIp(context);

        if (string.IsNullOrEmpty(clientIp) || clientIp == "127.0.0.1" || clientIp == "::1")
        {
            // localhost atla
            await _next(context);
            return;
        }

        // ── Cache kontrolü (hızlı) ──────────────────────────────────────────
        lock (_lock)
        {
            // Periyodik temizlik (her 10dk)
            if (DateTime.UtcNow - _lastCleanup > TimeSpan.FromMinutes(10))
            {
                var expired = _blockedIps.Where(kv => kv.Value < DateTime.UtcNow).Select(kv => kv.Key).ToList();
                foreach (var ip in expired) _blockedIps.Remove(ip);
                _lastCleanup = DateTime.UtcNow;
            }

            if (_blockedIps.TryGetValue(clientIp, out var blockUntil) && blockUntil > DateTime.UtcNow)
            {
                _logger.LogWarning("🚫 IP_BLOCKED: {IP} blocked until {Until}", clientIp, blockUntil);
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                context.Response.WriteAsJsonAsync(new
                {
                    error = "IP_BLOCKED",
                    message = "IP adresiniz şüpheli aktivite nedeniyle geçici olarak engellenmiştir.",
                    retryAfter = (int)(blockUntil - DateTime.UtcNow).TotalSeconds
                }).GetAwaiter().GetResult();
                return;
            }
        }

        // ── DB kontrolü (her auth isteğinde değil, sadece auth endpoint'lerinde) ──
        var path = context.Request.Path.Value ?? "";
        if (path.Contains("/auth/login") || path.Contains("/auth/register"))
        {
            var since = DateTime.UtcNow - CheckWindow;

            var bruteForceCount = await db.SecurityEvents
                .AsNoTracking()
                .CountAsync(se => se.IpAddress == clientIp
                    && se.EventType == "BRUTE_FORCE_DETECTED"
                    && se.CreatedAt >= since);

            var loginFailCount = await db.SecurityEvents
                .AsNoTracking()
                .CountAsync(se => se.IpAddress == clientIp
                    && se.EventType == "LOGIN_FAILED"
                    && se.CreatedAt >= since);

            if (bruteForceCount >= BruteForceThreshold || loginFailCount >= LoginFailThreshold)
            {
                lock (_lock)
                {
                    _blockedIps[clientIp] = DateTime.UtcNow + BlockDuration;
                }

                _logger.LogWarning(
                    "🚫 IP_AUTO_BLOCKED: {IP} | BruteForce: {BF} | LoginFail: {LF}",
                    clientIp, bruteForceCount, loginFailCount);

                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "IP_BLOCKED",
                    message = "IP adresiniz şüpheli aktivite nedeniyle geçici olarak engellenmiştir.",
                    retryAfter = (int)BlockDuration.TotalSeconds
                });
                return;
            }
        }

        await _next(context);
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();
        return context.Connection.RemoteIpAddress?.ToString() ?? "";
    }
}
