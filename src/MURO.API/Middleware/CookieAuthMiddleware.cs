namespace MURO.API.Middleware;

/// <summary>
/// HttpOnly cookie'den JWT token okur ve Authorization header'ına aktarır.
/// Bu sayede frontend localStorage kullanmak zorunda kalmaz — XSS ile token çalınamaz.
/// 
/// Öncelik sırası:
/// 1. Authorization header (varsa — SignalR, mobile app)
/// 2. muro_auth cookie (httpOnly, Secure, SameSite=Strict)
/// </summary>
public class CookieAuthMiddleware
{
    private readonly RequestDelegate _next;
    public const string CookieName = "muro_auth";

    public CookieAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Authorization header zaten varsa dokunma (mobile/API client)
        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            var cookie = context.Request.Cookies[CookieName];
            if (!string.IsNullOrEmpty(cookie))
            {
                context.Request.Headers.Append("Authorization", $"Bearer {cookie}");
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Login/Register sonrası JWT'yi httpOnly cookie olarak ayarlar.
    /// </summary>
    public static void SetAuthCookie(HttpContext context, string token, DateTime expiresAt)
    {
        context.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,                    // JavaScript erişemez (XSS koruması)
            Secure = !context.RequestServices    // Production'da Secure=true
                .GetRequiredService<IHostEnvironment>().IsDevelopment(),
            SameSite = SameSiteMode.Strict,      // CSRF koruması
            Expires = expiresAt,                 // JWT ile aynı süre
            Path = "/",                          // Tüm API path'leri
            IsEssential = true                   // GDPR: fonksiyonel cookie
        });
    }

    /// <summary>
    /// Logout'ta cookie'yi temizler.
    /// </summary>
    public static void ClearAuthCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Path = "/"
        });
    }
}
