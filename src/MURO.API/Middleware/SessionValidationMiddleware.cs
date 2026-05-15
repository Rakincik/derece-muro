using System.Security.Claims;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MURO.API.Middleware;

/// <summary>
/// Her authenticated API isteğinde JWT'deki sessionId'nin hâlâ aktif olup olmadığını doğrular.
/// 🚀 Redis-first: önce Redis'e bakar (&lt;0.5ms), cache miss'te DB'ye düşer.
/// Pasif veya silinmiş oturum → 401 SESSION_KICKED.
/// </summary>
public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;
    private readonly bool _skipValidation;

    public SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _skipValidation = env.EnvironmentName == "Testing";
    }

    public async Task InvokeAsync(HttpContext context, MuroDbContext db, ICacheService cache)
    {
        if (_skipValidation)
        {
            await _next(context);
            return;
        }

        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        var sessionIdClaim = context.User.FindFirst("sessionId")?.Value;

        if (string.IsNullOrEmpty(sessionIdClaim) || !Guid.TryParse(sessionIdClaim, out var sessionId))
        {
            await _next(context);
            return;
        }

        // 🚀 Redis-first session validation
        var cacheKey = $"session:active:{sessionId}";
        var isActive = await cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await db.DeviceSessions
                .AsNoTracking()
                .AnyAsync(s => s.Id == sessionId && s.IsActive);
        }, TimeSpan.FromMinutes(5));

        if (!isActive)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogWarning(
                "SESSION_KICKED: Pasif oturum tespit edildi. SessionId: {SessionId} | UserId: {UserId} | IP: {IP}",
                sessionId, userId, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "SESSION_KICKED",
                message = "Hesabınıza başka bir cihazdan giriş yapıldı. Lütfen tekrar giriş yapın."
            });
            return;
        }

        await _next(context);
    }
}
