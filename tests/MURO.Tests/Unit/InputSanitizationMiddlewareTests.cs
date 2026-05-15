using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using MURO.API.Middleware;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// InputSanitizationMiddleware için unit testler — XSS ve SQL injection
/// pattern tespiti, güvenli input'un geçirilmesi, GET/DELETE bypass,
/// büyük body bypass, body stream rewinding.
/// </summary>
public class InputSanitizationMiddlewareTests
{
    private readonly Mock<ILogger<InputSanitizationMiddleware>> _loggerMock = new();

    private async Task<int> SendRequest(string method, string contentType, string body)
    {
        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new InputSanitizationMiddleware(next, _loggerMock.Object);
        var context = new DefaultHttpContext();

        context.Request.Method = method;
        context.Request.ContentType = contentType;
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        return context.Response.StatusCode;
    }

    // ════════════════════════════════════════════════════════════════════════
    // XSS DETECTION
    // ════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("javascript:void(0)")]
    [InlineData("<img onerror='alert(1)' src='x'>")]
    [InlineData("<iframe src='evil.com'></iframe>")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("document.cookie")]
    [InlineData("eval('code')")]
    public async Task XssPayload_ShouldBlock(string payload)
    {
        var json = $"{{\"name\": \"{payload}\"}}";
        var statusCode = await SendRequest("POST", "application/json", json);
        statusCode.Should().Be(400);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SQL INJECTION DETECTION
    // ════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("'; DROP TABLE users;")]
    [InlineData("' OR 1=1")]
    [InlineData("UNION SELECT * FROM users")]
    [InlineData("'; DELETE FROM courses;")]
    [InlineData("; DROP TABLE sessions")]
    public async Task SqlInjectionPayload_ShouldBlock(string payload)
    {
        var json = $"{{\"query\": \"{payload}\"}}";
        var statusCode = await SendRequest("POST", "application/json", json);
        statusCode.Should().Be(400);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SAFE INPUT — PASS THROUGH
    // ════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("{\"name\": \"Ali\", \"age\": 25}")]
    [InlineData("{\"title\": \"Matematik Dersi\", \"description\": \"Konu: Türev & İntegral\"}")]
    [InlineData("{\"url\": \"https://example.com/page?q=test\"}")]
    [InlineData("{\"note\": \"Bugün 15:00'te toplantı var. & operatörünü kullanınız.\"}")]
    public async Task SafeInput_ShouldPassThrough(string json)
    {
        var statusCode = await SendRequest("POST", "application/json", json);
        statusCode.Should().Be(200);
    }

    // ════════════════════════════════════════════════════════════════════════
    // METHOD BYPASS — GET/DELETE taranmaz
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRequest_ShouldBypassSanitization()
    {
        var statusCode = await SendRequest("GET", "application/json", "<script>alert(1)</script>");
        statusCode.Should().Be(200);
    }

    [Fact]
    public async Task DeleteRequest_ShouldBypassSanitization()
    {
        var statusCode = await SendRequest("DELETE", "application/json", "<script>alert(1)</script>");
        statusCode.Should().Be(200);
    }

    // ════════════════════════════════════════════════════════════════════════
    // CONTENT TYPE BYPASS — sadece JSON taranır
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NonJsonContent_ShouldBypass()
    {
        var statusCode = await SendRequest("POST", "text/plain", "<script>alert(1)</script>");
        statusCode.Should().Be(200);
    }

    [Fact]
    public async Task MultipartContent_ShouldBypass()
    {
        var statusCode = await SendRequest("POST", "multipart/form-data", "<script>alert(1)</script>");
        statusCode.Should().Be(200);
    }

    // ════════════════════════════════════════════════════════════════════════
    // PUT/PATCH de taranmalı
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PutRequest_WithXss_ShouldBlock()
    {
        var statusCode = await SendRequest("PUT", "application/json", "{\"name\": \"<script>x</script>\"}");
        statusCode.Should().Be(400);
    }

    [Fact]
    public async Task PatchRequest_WithSqlInjection_ShouldBlock()
    {
        var statusCode = await SendRequest("PATCH", "application/json", "{\"q\": \"'; DROP TABLE users;\"}");
        statusCode.Should().Be(400);
    }

    // ════════════════════════════════════════════════════════════════════════
    // EMPTY BODY
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EmptyBody_ShouldPassThrough()
    {
        bool nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new InputSanitizationMiddleware(next, _loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = 0;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    // ════════════════════════════════════════════════════════════════════════
    // RESPONSE FORMAT
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BlockedRequest_ShouldReturnJsonError()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new InputSanitizationMiddleware(next, _loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        var payloadBytes = Encoding.UTF8.GetBytes("{\"x\": \"<script>alert(1)</script>\"}");
        context.Request.Body = new MemoryStream(payloadBytes);
        context.Request.ContentLength = payloadBytes.Length;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
        context.Response.ContentType.Should().Contain("application/json");
    }
}
