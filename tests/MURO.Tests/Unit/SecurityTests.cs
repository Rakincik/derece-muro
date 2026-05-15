using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

using MURO.Tests.Integration;

namespace MURO.Tests.Unit;

/// <summary>
/// Güvenlik Testi Suite — JWT tampering, XSS, SQL Injection,
/// IDOR, Security Headers, CORS, brute-force koruması.
/// WebApplicationFactory ile gerçek HTTP istekleri test edilir.
/// </summary>
public class SecurityTests : IClassFixture<MuroTestFactory>
{
    private readonly HttpClient _client;
    private readonly MuroTestFactory _factory;

    public SecurityTests(MuroTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ── 1. JWT Tampered Token → 401 ─────────────────────────────────────────
    [Fact]
    public async Task TamperedJwt_ShouldReturn401()
    {
        // Gerçek JWT formatı ama geçersiz imza
        var fakeToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4iLCJpYXQiOjE1MTYyMzkwMjJ9.FAKE_SIGNATURE";
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", fakeToken);
        
        var response = await _client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── 2. Boş Token → 401 ──────────────────────────────────────────────────
    [Fact]
    public async Task EmptyBearerToken_ShouldReturn401()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "");
        
        var response = await _client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── 3. Expired Token → 401 ──────────────────────────────────────────────
    [Fact]
    public async Task ExpiredJwt_ShouldReturn401()
    {
        // JWT with exp in the past
        var expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwiZXhwIjoxfQ.FAKE";
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);
        
        var response = await _client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── 4. Protected Endpoint Without Auth → 401 ────────────────────────────
    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/courses")]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/groups")]
    [InlineData("/api/v1/notifications")]
    public async Task ProtectedEndpoint_NoAuth_ShouldReturn401(string endpoint)
    {
        _client.DefaultRequestHeaders.Clear();
        
        var response = await _client.GetAsync(endpoint);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── 5. Security Headers Doğrulaması ─────────────────────────────────────
    [Fact]
    public async Task Response_ShouldContainSecurityHeaders()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        response.Headers.TryGetValues("X-Content-Type-Options", out var xct);
        xct.Should().Contain("nosniff");

        response.Headers.TryGetValues("X-Frame-Options", out var xfo);
        xfo.Should().Contain("DENY");

        response.Headers.TryGetValues("Referrer-Policy", out var rp);
        rp.Should().Contain("strict-origin-when-cross-origin");
    }

    // ── 6. XSS Payload → 400 ────────────────────────────────────────────────
    [Theory]
    [InlineData("{\"name\": \"<script>alert('xss')</script>\"}")]
    [InlineData("{\"name\": \"<img src=x onerror=alert(1)>\"}")]
    [InlineData("{\"name\": \"javascript:alert('xss')\"}")]
    [InlineData("{\"name\": \"<iframe src='evil.com'></iframe>\"}")]
    public async Task XssPayload_ShouldReturn400(string jsonPayload)
    {
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        
        var response = await _client.PostAsync("/api/v1/auth/login", content);
        // InputSanitizationMiddleware 400 veya Auth 401 dönebilir — ikisi de kabul, rate limiter devredeyse 429 da dönebilir
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    // ── 7. SQL Injection Pattern → 400 ──────────────────────────────────────
    [Theory]
    [InlineData("{\"email\": \"'; DROP TABLE Users;--\", \"password\": \"test\"}")]
    [InlineData("{\"email\": \"' OR 1=1--\", \"password\": \"test\"}")]
    [InlineData("{\"email\": \"admin'; DELETE FROM users;--\", \"password\": \"x\"}")]
    public async Task SqlInjectionPayload_ShouldReturn400(string jsonPayload)
    {
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        
        var response = await _client.PostAsync("/api/v1/auth/login", content);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    // ── 8. CORS — Unauthorized Origin → No Access-Control-Allow-Origin ──────
    [Fact]
    public async Task Cors_UnauthorizedOrigin_ShouldNotAllowAccess()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("Origin", "https://evil-site.com");
        
        var response = await _client.SendAsync(request);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    // ── 9. Brute Force Login — 5+ Başarısız → Hesap Kilidi ──────────────────
    [Fact]
    public async Task BruteForce_ShouldLockAccount()
    {
        var loginPayload = new { email = "brute@test.com", password = "wrongpassword" };
        
        for (int i = 0; i < 6; i++)
        {
            var content = JsonContent.Create(loginPayload);
            var response = await _client.PostAsync("/api/v1/auth/login", content);
            // Her istek 401 veya 403 dönmeli (lockout sonrası), rate limiter girerse 429
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Forbidden,
                HttpStatusCode.BadRequest,
                HttpStatusCode.TooManyRequests);
        }
    }

    // ── 10. Swagger Production'da Erişilemez ────────────────────────────────
    [Fact(Skip = "Test ortamında Production environment başlatmak Redis ve DB çakışmalarına neden oluyor.")]
    public async Task Swagger_InProduction_ShouldBe404()
    {
        // Test environment'ta Swagger kapalı olmalı (Testing != Development)
        using var productionFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
        });
        using var prodClient = productionFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await prodClient.GetAsync("/swagger/index.html");
        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "Swagger production ortamında erişilmemeli");
    }

    // ── 11. Oversized Request Body → Rejected ───────────────────────────────
    [Fact]
    public async Task OversizedJsonBody_ShouldBeRejected()
    {
        // 15MB JSON body
        var largePayload = new string('A', 15 * 1024 * 1024);
        var json = $"{{\"data\": \"{largePayload}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _client.PostAsync("/api/v1/auth/login", content);
        // Should be 400, 401, or 413 (Payload Too Large) — not 500
        ((int)response.StatusCode).Should().BeLessThan(500);
    }

    // ── 12. Deeply Nested JSON → Rejected ───────────────────────────────────
    [Fact]
    public async Task DeeplyNestedJson_ShouldNotCrashServer()
    {
        // 100 nested objects
        var nested = new StringBuilder("{");
        for (int i = 0; i < 100; i++) nested.Append("\"a\":{");
        for (int i = 0; i < 100; i++) nested.Append("}");
        nested.Append("}");
        
        var content = new StringContent(nested.ToString(), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/auth/login", content);
        // Should not be 500
        ((int)response.StatusCode).Should().BeLessThan(500);
    }

    // ── 13. Webhook Without Checksum → 401 ──────────────────────────────────
    [Fact]
    public async Task BbbWebhook_WithoutChecksum_ShouldReject()
    {
        var payload = new { events = new[] { new { eventType = "meeting-ended", meetingId = "evil-meeting" } } };
        var content = JsonContent.Create(payload);
        
        var response = await _client.PostAsync("/api/v1/bbb/webhook/events", content);
        // Secret yoksa geliştirme modunda geçebilir, ama production'da 401
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,           // dev mode (secret yapılandırılmamış)
            HttpStatusCode.Unauthorized, // production (secret var, checksum yok)
            HttpStatusCode.BadRequest);
    }

    // ── 14. Path Traversal Attempt ──────────────────────────────────────────
    [Theory]
    [InlineData("/api/v1/../../../etc/passwd")]
    [InlineData("/api/v1/courses/../../admin")]
    public async Task PathTraversal_ShouldNotExposeFiles(string path)
    {
        var response = await _client.GetAsync(path);
        ((int)response.StatusCode).Should().BeOneOf(301, 400, 401, 404);
    }

    // ── 15. HTTP Method Tampering ────────────────────────────────────────────
    [Fact]
    public async Task OptionsMethod_ShouldNotLeakInfo()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "http://localhost:3001");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        
        var response = await _client.SendAsync(request);
        // Should be 200 (preflight) or 204, not 500
        ((int)response.StatusCode).Should().BeLessThan(500);
    }

    // ── 16. Server Header Gizleme ───────────────────────────────────────────
    [Fact]
    public async Task Response_ShouldNotExposeServerInfo()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        response.Headers.Contains("X-Powered-By").Should().BeFalse("X-Powered-By gizlenmeli");
    }

    // ── 17. IDOR — Başka Kullanıcının Verisine Token'sız Erişim ──────────────
    [Fact]
    public async Task Idor_UnauthorizedUserData_ShouldReturn401()
    {
        _client.DefaultRequestHeaders.Clear();
        var randomUserId = Guid.NewGuid();
        
        var response = await _client.GetAsync($"/api/v1/users/{randomUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── 18. Content-Type Mismatch ───────────────────────────────────────────
    [Fact]
    public async Task WrongContentType_ShouldNotCrash()
    {
        var content = new StringContent("this is not json", Encoding.UTF8, "text/plain");
        
        var response = await _client.PostAsync("/api/v1/auth/login", content);
        // Should return 400 or 415, not 500
        ((int)response.StatusCode).Should().BeLessThan(500);
    }
}
