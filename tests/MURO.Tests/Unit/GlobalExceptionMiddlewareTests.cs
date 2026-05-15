using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using MURO.API.Middleware;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// GlobalExceptionMiddleware için unit testler — Exception → HTTP status code mapping,
/// JSON response format doğrulaması, traceId içermesi, response body kontrolü.
/// </summary>
public class GlobalExceptionMiddlewareTests
{
    private readonly Mock<ILogger<GlobalExceptionMiddleware>> _loggerMock = new();

    private (GlobalExceptionMiddleware middleware, DefaultHttpContext context) CreateMiddleware(
        RequestDelegate next)
    {
        var middleware = new GlobalExceptionMiddleware(next, _loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return (middleware, context);
    }

    private async Task<(int statusCode, JsonDocument body)> ExecuteWithException(Exception ex)
    {
        RequestDelegate next = _ => throw ex;
        var (middleware, ctx) = CreateMiddleware(next);

        await middleware.InvokeAsync(ctx);

        ctx.Response.Body.Position = 0;
        var json = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        var doc = JsonDocument.Parse(json);

        return (ctx.Response.StatusCode, doc);
    }

    // ════════════════════════════════════════════════════════════════════════
    // EXCEPTION → STATUS CODE MAPPING
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task KeyNotFoundException_ShouldReturn404()
    {
        var (status, body) = await ExecuteWithException(new KeyNotFoundException("Bulunamadı"));
        status.Should().Be(404);
        body.RootElement.GetProperty("error").GetString().Should().Be("Bulunamadı");
    }

    [Fact]
    public async Task InvalidOperationException_ShouldReturn400()
    {
        var (status, body) = await ExecuteWithException(new InvalidOperationException("Geçersiz işlem"));
        status.Should().Be(400);
        body.RootElement.GetProperty("error").GetString().Should().Be("Geçersiz işlem");
    }

    [Fact]
    public async Task UnauthorizedAccessException_ShouldReturn403()
    {
        var (status, body) = await ExecuteWithException(new UnauthorizedAccessException("Yetkisiz"));
        status.Should().Be(403);
        body.RootElement.GetProperty("error").GetString().Should().Be("Yetkisiz");
    }

    [Fact]
    public async Task ArgumentException_ShouldReturn400()
    {
        var (status, body) = await ExecuteWithException(new ArgumentException("Hatalı parametre"));
        status.Should().Be(400);
    }

    [Fact]
    public async Task GenericException_ShouldReturn500()
    {
        var (status, body) = await ExecuteWithException(new Exception("Bilinmeyen hata"));
        status.Should().Be(500);
        // Gerçek hata mesajı client'a açılmamalı
        body.RootElement.GetProperty("error").GetString()
            .Should().Contain("Sunucu hatası");
    }

    // ════════════════════════════════════════════════════════════════════════
    // JSON RESPONSE FORMAT
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Response_ShouldContainTraceId()
    {
        var (_, body) = await ExecuteWithException(new KeyNotFoundException("X"));
        body.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Response_ShouldContainTimestamp()
    {
        var (_, body) = await ExecuteWithException(new KeyNotFoundException("X"));
        body.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Response_ShouldContainStatusCode()
    {
        var (_, body) = await ExecuteWithException(new KeyNotFoundException("X"));
        body.RootElement.GetProperty("statusCode").GetInt32().Should().Be(404);
    }

    [Fact]
    public async Task Response_ContentType_ShouldBeJson()
    {
        RequestDelegate next = _ => throw new KeyNotFoundException("X");
        var (middleware, ctx) = CreateMiddleware(next);
        await middleware.InvokeAsync(ctx);
        ctx.Response.ContentType.Should().Be("application/json");
    }

    // ════════════════════════════════════════════════════════════════════════
    // HAPPY PATH
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NoException_ShouldPassThrough()
    {
        bool nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var (middleware, ctx) = CreateMiddleware(next);

        await middleware.InvokeAsync(ctx);

        nextCalled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(200);
    }
}
