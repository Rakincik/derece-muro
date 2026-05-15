using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MURO.Infrastructure.Persistence;

namespace MURO.API.Middleware;

/// <summary>
/// Structured request logging middleware.
/// Her HTTP isteğini okunabilir kullanıcı adı, kurum adı, IP ile loglar.
/// Performans eşikleri: 500ms → WARN, 2000ms → ERROR.
/// </summary>
public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    private const int WarnThresholdMs = 500;
    private const int ErrorThresholdMs = 2000;

    // Loglama dışı tutulacak path'ler (static dosyalar, swagger vs.)
    private static readonly string[] SkipPrefixes =
        ["/swagger", "/favicon", "/health/live", "/_framework"];
    private static readonly string[] SkipSuffixes = [".js", ".css", ".map", ".woff2", ".png", ".ico"];

    // Tenant ID → Tenant Name cache (restart'a kadar kalır)
    private static readonly ConcurrentDictionary<string, string> TenantNameCache = new();

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Static dosyaları atla
        if (ShouldSkip(path))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();

        await _next(context);

        sw.Stop();
        var elapsed = sw.ElapsedMilliseconds;
        var method = context.Request.Method;
        var statusCode = context.Response.StatusCode;

        // ── Okunabilir Context ──────────────────────────────────────────
        var userDisplay = GetUserDisplay(context);
        var tenantDisplay = await GetTenantDisplayAsync(context);
        var correlationId = context.Items["CorrelationId"]?.ToString();
        var clientIp = GetClientIp(context);
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault()?[..Math.Min(context.Request.Headers.UserAgent.FirstOrDefault()?.Length ?? 0, 100)];
        var contentLength = context.Response.ContentLength ?? 0;

        // ── Log Seviyesi Seçimi ─────────────────────────────────────────
        if (elapsed >= ErrorThresholdMs)
        {
            _logger.LogError(
                "🐢 {Method} {Path} → {StatusCode} in {Elapsed}ms | {User} @ {Tenant} | IP:{ClientIp} CorrId:{CorrelationId} Size:{ContentLength}B UA:{UserAgent}",
                method, path, statusCode, elapsed, userDisplay, tenantDisplay, clientIp, correlationId, contentLength, userAgent);
        }
        else if (elapsed >= WarnThresholdMs)
        {
            _logger.LogWarning(
                "⚠️ {Method} {Path} → {StatusCode} in {Elapsed}ms | {User} @ {Tenant} | IP:{ClientIp} CorrId:{CorrelationId}",
                method, path, statusCode, elapsed, userDisplay, tenantDisplay, clientIp, correlationId);
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning(
                "❌ {Method} {Path} → {StatusCode} in {Elapsed}ms | {User} @ {Tenant} | IP:{ClientIp} CorrId:{CorrelationId}",
                method, path, statusCode, elapsed, userDisplay, tenantDisplay, clientIp, correlationId);
        }
        else
        {
            _logger.LogInformation(
                "✅ {Method} {Path} → {StatusCode} in {Elapsed}ms | {User} @ {Tenant}",
                method, path, statusCode, elapsed, userDisplay, tenantDisplay);
        }
    }

    /// <summary>Kullanıcı bilgisini JWT claim'lerinden oku → "Ad Soyad (email)" veya "Anonim"</summary>
    private static string GetUserDisplay(HttpContext context)
    {
        var email = context.User.FindFirstValue(ClaimTypes.Email)
                 ?? context.User.FindFirstValue("email");
        var name = context.User.FindFirstValue(ClaimTypes.Name)
                ?? context.User.FindFirstValue("name");
        var firstName = context.User.FindFirstValue(ClaimTypes.GivenName)
                     ?? context.User.FindFirstValue("firstName");
        var lastName = context.User.FindFirstValue(ClaimTypes.Surname)
                    ?? context.User.FindFirstValue("lastName");

        // Ad Soyad varsa birleştir
        var fullName = !string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName)
            ? $"{firstName} {lastName}".Trim()
            : name;

        if (!string.IsNullOrEmpty(fullName) && !string.IsNullOrEmpty(email))
            return $"{fullName} ({email})";
        if (!string.IsNullOrEmpty(email))
            return email;
        if (!string.IsNullOrEmpty(fullName))
            return fullName;

        return "Anonim";
    }

    /// <summary>Tenant adını header'dan alıp DB'den çöz, cache'le.</summary>
    private async Task<string> GetTenantDisplayAsync(HttpContext context)
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                    ?? context.User.FindFirst("tenantId")?.Value;

        if (string.IsNullOrEmpty(tenantId))
            return "-";

        // Cache'te varsa direkt dön
        if (TenantNameCache.TryGetValue(tenantId, out var cached))
            return cached;

        // DB'den çek
        try
        {
            var db = context.RequestServices.GetService<MuroDbContext>();
            if (db != null && Guid.TryParse(tenantId, out var tid))
            {
                var tenantName = await db.Tenants
                    .AsNoTracking()
                    .Where(t => t.Id == tid)
                    .Select(t => t.Name)
                    .FirstOrDefaultAsync();

                var display = tenantName ?? tenantId[..8];
                TenantNameCache.TryAdd(tenantId, display);
                return display;
            }
        }
        catch { /* fallback to ID */ }

        var fallback = tenantId.Length > 8 ? tenantId[..8] + "…" : tenantId;
        TenantNameCache.TryAdd(tenantId, fallback);
        return fallback;
    }

    private static bool ShouldSkip(string path)
    {
        foreach (var prefix in SkipPrefixes)
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var suffix in SkipSuffixes)
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
