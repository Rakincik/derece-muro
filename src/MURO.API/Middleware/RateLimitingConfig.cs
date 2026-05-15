using System.Threading.RateLimiting;

namespace MURO.API.Middleware;

/// <summary>
/// 3 katmanlı rate limiting:
/// 1. Global → 300 istek/dakika/IP (DDoS koruması)
/// 2. Auth   → 10 istek/dakika/IP (brute-force koruması)
/// 3. API    → 200 istek/dakika/kullanıcı (abuse koruması)
/// 
/// Not: Dashboard açılışta ~15-20 paralel istek atar (groups, users, notifications,
/// branding, calendar, packages vb.). Eski 60/dakika limiti normal kullanımı engelliyordu.
/// </summary>
public static class RateLimitingConfig
{
    public const string GlobalPolicy = "global";
    public const string AuthPolicy = "auth";
    public const string ApiPolicy = "api";

    public static IServiceCollection AddMuroRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // 429 Too Many Requests dönecek
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
                    ? retryAfterValue.TotalSeconds : 60;
                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter).ToString();
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "TOO_MANY_REQUESTS",
                    message = $"Çok fazla istek gönderdiniz. {(int)retryAfter} saniye sonra tekrar deneyin.",
                    retryAfterSeconds = (int)retryAfter,
                    timestamp = DateTime.UtcNow
                }, ct);
            };

            // ── 1. GLOBAL: Her IP için 300 istek/dakika ─────────────────────
            options.AddPolicy(GlobalPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIp(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // ── 2. AUTH: Login/Register için 10 istek/dakika/IP ─────────────
            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: GetClientIp(context),
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 2,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // ── 3. API: Authenticated user başına 200 istek/dakika ──────────
            options.AddPolicy(ApiPolicy, context =>
            {
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: userId ?? GetClientIp(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 200,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }

    private static string GetClientIp(HttpContext context)
    {
        // X-Forwarded-For header'ı varsa (reverse proxy arkasında)
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
