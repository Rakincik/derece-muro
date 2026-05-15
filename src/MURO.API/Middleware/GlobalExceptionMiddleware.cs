using System.Diagnostics;
using System.Text.Json;
using MURO.Domain.Exceptions;
using MuroValidationException = MURO.Domain.Exceptions.ValidationException;

namespace MURO.API.Middleware;

/// <summary>
/// Merkezi hata yönetimi middleware'i.
/// MuroException hiyerarşisini tanır ve uygun HTTP response döner.
/// Tüm hatalar RFC 7807 Problem Details benzeri formatta döner.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (MuroException ex)
        {
            // Bilinen exception — uygun seviyede logla
            if (ex.StatusCode >= 500)
                _logger.LogError(ex, "[{Method} {Path}] {ErrorCode}: {Message}",
                    context.Request.Method, context.Request.Path, ex.ErrorCode, ex.Message);
            else
                _logger.LogWarning("[{Method} {Path}] {ErrorCode}: {Message}",
                    context.Request.Method, context.Request.Path, ex.ErrorCode, ex.Message);

            await WriteErrorResponse(context, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Auth katmanından gelen exception'lar
            _logger.LogWarning("[{Method} {Path}] AUTH: {Message}",
                context.Request.Method, context.Request.Path, ex.Message);
            await WriteErrorResponse(context, 403, "FORBIDDEN", ex.Message);
        }
        catch (MURO.Application.Exceptions.QuotaExceededException ex)
        {
            // Kota aşımı durumu (HTTP 402 Payment Required veya 403 Forbidden kullanılabilir)
            _logger.LogWarning("[{Method} {Path}] QUOTA_EXCEEDED: {Message}",
                context.Request.Method, context.Request.Path, ex.Message);
            await WriteErrorResponse(context, 402, "QUOTA_EXCEEDED", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            // Legacy: Eski servislerden gelen KeyNotFoundException'lar
            _logger.LogWarning("[{Method} {Path}] NOT_FOUND: {Message}",
                context.Request.Method, context.Request.Path, ex.Message);
            await WriteErrorResponse(context, 404, "RESOURCE_NOT_FOUND", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Legacy: Eski servislerden gelen InvalidOperationException'lar
            _logger.LogWarning("[{Method} {Path}] BAD_REQUEST: {Message}",
                context.Request.Method, context.Request.Path, ex.Message);
            await WriteErrorResponse(context, 400, "BAD_REQUEST", ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("[{Method} {Path}] BAD_REQUEST: {Message}",
                context.Request.Method, context.Request.Path, ex.Message);
            await WriteErrorResponse(context, 400, "BAD_REQUEST", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Method} {Path}] UNHANDLED: Beklenmeyen hata",
                context.Request.Method, context.Request.Path);
            await WriteErrorResponse(context, 500, "INTERNAL_ERROR",
                "Sunucu hatası oluştu. Lütfen daha sonra tekrar deneyiniz.");
        }
    }

    /// <summary>MuroException → JSON</summary>
    private static async Task WriteErrorResponse(HttpContext context, MuroException ex)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = ex.StatusCode;
        context.Response.ContentType = "application/json";

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        object response;

        if (ex is MuroValidationException validationEx)
        {
            // Doğrulama hataları alan bazlı detay içerir
            response = new
            {
                error = ex.Message,
                errorCode = ex.ErrorCode,
                statusCode = ex.StatusCode,
                errors = validationEx.Errors,
                traceId,
                timestamp = DateTime.UtcNow
            };
        }
        else
        {
            response = new
            {
                error = ex.Message,
                errorCode = ex.ErrorCode,
                statusCode = ex.StatusCode,
                traceId,
                timestamp = DateTime.UtcNow
            };
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    /// <summary>Generic error → JSON</summary>
    private static async Task WriteErrorResponse(HttpContext context, int statusCode, string errorCode, string message)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var response = new
        {
            error = message,
            errorCode,
            statusCode,
            traceId,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
