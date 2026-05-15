namespace MURO.API.Middleware;

/// <summary>
/// Her HTTP yanıtına güvenlik header'ları ekler.
/// XSS, Clickjacking, MIME sniffing, Referer sızıntısı ve diğer saldırılara karşı koruma.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        bool isUploadPath = path.StartsWith("/api/v1/uploads", StringComparison.OrdinalIgnoreCase) || 
                            path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase);

        // ── XSS Koruması ───────────────────────────────────────────────────
        // Tarayıcının Content-Type'ı tahmin etmesini engelle
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Eski tarayıcılar için XSS filtresi
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

        if (!isUploadPath)
        {
            // ── Clickjacking Koruması ──────────────────────────────────────────
            // Sayfanın iframe içinde gösterilmesini engelle
            context.Response.Headers["X-Frame-Options"] = "DENY";

            // ── Referer Sızıntı Engeli ─────────────────────────────────────────
            // Aynı origin dışında tam URL gönderme
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // ── İzin Politikası ────────────────────────────────────────────────
            // Kamera, mikrofon, konum gibi API'lere erişimi kısıtla
            context.Response.Headers["Permissions-Policy"] = 
                "camera=(), microphone=(), geolocation=(), payment=()";

            // ── Content Security Policy ────────────────────────────────────────
            // API olduğu için sıkı CSP — sadece 'self' kaynaklarına izin ver
            context.Response.Headers["Content-Security-Policy"] = 
                "default-src 'none'; frame-ancestors 'none'";

            // ── HTTPS Zorunluluk (Production) ──────────────────────────────────
            // 1 yıl boyunca HTTPS'ye yönlendir
            context.Response.Headers["Strict-Transport-Security"] = 
                "max-age=31536000; includeSubDomains; preload";

            // ── Cache Koruması ─────────────────────────────────────────────────
            // API yanıtlarını cache'leme (hassas veri sızıntısı engeli)
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
        }

        // ── Server Bilgisi Gizleme ─────────────────────────────────────────
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");

        await _next(context);
    }
}
