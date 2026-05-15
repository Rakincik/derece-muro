namespace MURO.API.Middleware;

/// <summary>
/// Her isteğe benzersiz bir Correlation ID ekler.
/// Gelen header'da varsa onu kullanır, yoksa yeni üretir.
/// Tüm loglarda bu ID ile istek takibi yapılabilir.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderName = "X-Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Gelen header varsa onu kullan, yoksa yeni üret
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N")[..12]; // Kısa, okunabilir ID
        }

        // HttpContext'e kaydet (diğer middleware'ler ve servisler kullanabilsin)
        context.Items["CorrelationId"] = correlationId.ToString();

        // Response header'a da ekle (client'lar takip edebilsin)
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Serilog LogContext'e ekle — tüm loglar bu ID'yi taşır
        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId.ToString()))
        {
            await _next(context);
        }
    }
}
