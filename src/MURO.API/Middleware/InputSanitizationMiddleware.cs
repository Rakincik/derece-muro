using System.Text;
using System.Text.RegularExpressions;

namespace MURO.API.Middleware;

/// <summary>
/// JSON body'deki XSS ve SQL injection pattern'lerini tespit eder.
/// Küçük boyutlu isteklerde (&lt; 1MB) body'yi okuyup tarar.
/// Tehlikeli pattern bulursa → 400 Bad Request + security log.
/// </summary>
public class InputSanitizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputSanitizationMiddleware> _logger;

    // XSS vektörleri
    private static readonly Regex XssPattern = new(
        @"<script[\s>]|javascript\s*:|on\w+\s*=|<iframe|<object|<embed|<form\s|<svg\s*onload|document\.cookie|eval\s*\(|alert\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100)); // Regex DoS koruması

    // SQL injection vektörleri (bilinen tehlikeli kalıplar)
    private static readonly Regex SqlInjectionPattern = new(
        @"(';\s*(DROP|DELETE|UPDATE|INSERT|ALTER|EXEC|UNION)\s)|(-{2}\s*$)|(;\s*DROP\s+TABLE)|(\bOR\b\s+1\s*=\s*1)|(\bUNION\b\s+\bSELECT\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private const int MaxScanSize = 1 * 1024 * 1024; // 1MB'dan büyük body taranmaz

    public InputSanitizationMiddleware(RequestDelegate next, ILogger<InputSanitizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Sadece JSON body olan POST/PUT/PATCH isteklerini tara
        if (IsWriteMethod(context.Request.Method) && IsJsonContent(context.Request.ContentType))
        {
            // Body'yi okunabilir yap (buffering)
            context.Request.EnableBuffering();

            if (context.Request.ContentLength > 0 && context.Request.ContentLength < MaxScanSize)
            {
                var body = await ReadBodyAsync(context.Request);

                if (!string.IsNullOrEmpty(body))
                {
                    // ── XSS Taraması ───────────────────────────────────────
                    if (XssPattern.IsMatch(body))
                    {
                        var ip = context.Connection.RemoteIpAddress?.ToString();
                        _logger.LogWarning(
                            "🔴 XSS_ATTEMPT_BLOCKED | IP: {IP} | Path: {Path} | Method: {Method}",
                            ip, context.Request.Path, context.Request.Method);

                        context.Response.StatusCode = 400;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "MALICIOUS_INPUT_DETECTED",
                            message = "İstekte potansiyel olarak zararlı içerik tespit edildi.",
                            timestamp = DateTime.UtcNow
                        });
                        return;
                    }

                    // ── SQL Injection Taraması ──────────────────────────────
                    if (SqlInjectionPattern.IsMatch(body))
                    {
                        var ip = context.Connection.RemoteIpAddress?.ToString();
                        _logger.LogWarning(
                            "🔴 SQL_INJECTION_ATTEMPT | IP: {IP} | Path: {Path} | Method: {Method}",
                            ip, context.Request.Path, context.Request.Method);

                        context.Response.StatusCode = 400;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "MALICIOUS_INPUT_DETECTED",
                            message = "İstekte potansiyel olarak zararlı içerik tespit edildi.",
                            timestamp = DateTime.UtcNow
                        });
                        return;
                    }
                }

                // Body stream'i başa sar (downstream middleware okuyabilsin)
                context.Request.Body.Position = 0;
            }
        }

        await _next(context);
    }

    private static bool IsWriteMethod(string method) =>
        method is "POST" or "PUT" or "PATCH";

    private static bool IsJsonContent(string? contentType) =>
        contentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }
}
