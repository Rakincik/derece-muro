using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Middleware zinciri E2E — Tüm middleware katmanlarının gerçek HTTP istekleriyle
/// birlikte çalışmasını doğrular. JWT auth, XSS/SQL injection blocking,
/// exception handling, security headers, path traversal, CORS, method tampering.
/// </summary>
[Trait("Category", "E2E")]
public class MiddlewareE2ETests : IClassFixture<MuroTestFactory>, IAsyncLifetime
{
    private readonly MuroTestFactory _f;

    public MiddlewareE2ETests(MuroTestFactory factory) => _f = factory;
    public async Task InitializeAsync() => await _f.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ════════════════════════════════════════════════════════════════════════
    // AUTH MIDDLEWARE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NoToken_ShouldReturn401()
    {
        var res = await _f.CreateAnonymousClient().GetAsync("/api/v1/courses");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TamperedJwt_ShouldReturn401()
    {
        var client = _f.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "abc.def.ghi");
        var res = await client.GetAsync("/api/v1/courses");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredLikeJwt_ShouldReturn401()
    {
        var client = _f.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                "eyJhbGciOiJIUzI1NiJ9.eyJleHAiOjF9.invalid-sig");
        var res = await client.GetAsync("/api/v1/courses");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidToken_ShouldPassThrough()
    {
        var res = await _f.CreateAdminClient().GetAsync("/api/v1/courses");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ════════════════════════════════════════════════════════════════════════
    // INPUT SANITIZATION — XSS
    // ════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<iframe src='evil.com'></iframe>")]
    [InlineData("<img onerror='alert(1)' src='x'>")]
    [InlineData("javascript:void(0)")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("document.cookie")]
    [InlineData("eval('malicious()')")]
    public async Task XssPayload_InBody_ShouldReturn400(string xss)
    {
        var admin = _f.CreateAdminClient();
        var payload = new StringContent(
            $"{{\"title\": \"{xss}\", \"description\": \"test\"}}",
            Encoding.UTF8, "application/json");
        var res = await admin.PostAsync("/api/v1/courses", payload);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ════════════════════════════════════════════════════════════════════════
    // INPUT SANITIZATION — SQL INJECTION
    // ════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("'; DROP TABLE courses;")]
    [InlineData("' OR 1=1")]
    [InlineData("UNION SELECT * FROM users")]
    [InlineData("'; DELETE FROM sessions;")]
    [InlineData("; DROP TABLE users")]
    public async Task SqlInjectionPayload_InBody_ShouldReturn400(string sql)
    {
        var admin = _f.CreateAdminClient();
        var payload = new StringContent(
            $"{{\"title\": \"{sql}\", \"description\": \"test\"}}",
            Encoding.UTF8, "application/json");
        var res = await admin.PostAsync("/api/v1/courses", payload);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ════════════════════════════════════════════════════════════════════════
    // INPUT SANITIZATION — GÜVENLİ İÇERİK GEÇMELİ
    // ════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Normal kurs adı")]
    [InlineData("Matematik & İstatistik")]
    [InlineData("Kurs (2024-2025)")]
    [InlineData("50% + 50% = 100%")]
    [InlineData("C# ile Programlama")]
    public async Task SafeInput_ShouldPassThrough(string safeTitle)
    {
        var admin = _f.CreateAdminClient();
        var res = await admin.PostAsJsonAsync("/api/v1/courses",
            new MURO.Application.DTOs.Courses.CreateCourseRequest(safeTitle, "Güvenli", null, "Online", null, null));
        ((int)res.StatusCode).Should().BeLessThan(400);
    }

    // ════════════════════════════════════════════════════════════════════════
    // INPUT SANITIZATION — GET İSTEKLERİ BYPASS
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRequest_WithXssQueryParam_ShouldNotBlock()
    {
        var admin = _f.CreateAdminClient();
        // GET istekleri body taramaz — query param XSS engellemez (bu doğru davranış)
        var res = await admin.GetAsync("/api/v1/courses?search=<script>");
        // Bloklanmamalı (GET body yok)
        res.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }

    // ════════════════════════════════════════════════════════════════════════
    // INPUT SANITIZATION — PUT/PATCH KORUNMALI
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PutRequest_WithXss_ShouldBlock()
    {
        var admin = _f.CreateAdminClient();
        var payload = new StringContent(
            "{\"title\": \"<script>x</script>\", \"description\": \"y\"}",
            Encoding.UTF8, "application/json");
        var res = await admin.PutAsync($"/api/v1/courses/{Guid.NewGuid()}", payload);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GLOBAL EXCEPTION MIDDLEWARE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NonExistentResource_ShouldReturn404Json()
    {
        var admin = _f.CreateAdminClient();
        var res = await admin.GetAsync($"/api/v1/courses/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("error");
    }

    [Fact]
    public async Task NonExistentExam_ShouldReturn404Json()
    {
        var admin = _f.CreateAdminClient();
        var res = await admin.GetAsync($"/api/v1/exams/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("error");
    }

    [Fact]
    public async Task NonExistentAssignment_ShouldReturn404Json()
    {
        var admin = _f.CreateAdminClient();
        var res = await admin.GetAsync($"/api/v1/assignments/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SECURITY HEADERS
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Response_ShouldHaveXContentTypeOptions()
    {
        var res = await _f.CreateAdminClient().GetAsync("/api/v1/courses");
        res.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
        res.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");
    }

    [Fact]
    public async Task Response_ShouldHaveXFrameOptions()
    {
        var res = await _f.CreateAdminClient().GetAsync("/api/v1/courses");
        res.Headers.Contains("X-Frame-Options").Should().BeTrue();
    }

    [Fact]
    public async Task Response_ShouldNotExposeServerInfo()
    {
        var res = await _f.CreateAdminClient().GetAsync("/api/v1/courses");
        if (res.Headers.Contains("Server"))
        {
            var server = res.Headers.GetValues("Server").FirstOrDefault() ?? "";
            server.Should().NotContain("Kestrel");
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // PATH TRAVERSAL
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PathTraversal_EtcPasswd_ShouldFail()
    {
        var res = await _f.CreateAnonymousClient().GetAsync("/api/v1/../../etc/passwd");
        res.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task PathTraversal_WindowsConfig_ShouldFail()
    {
        var res = await _f.CreateAnonymousClient().GetAsync("/api/v1/..\\..\\web.config");
        res.IsSuccessStatusCode.Should().BeFalse();
    }

    // ════════════════════════════════════════════════════════════════════════
    // ÇOKLU ENDPOINT AUTH KONTROLÜ
    // ════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/api/v1/courses")]
    [InlineData("/api/v1/exams")]
    [InlineData("/api/v1/assignments")]
    [InlineData("/api/v1/questions")]
    [InlineData("/api/v1/calendar")]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/v1/media")]
    [InlineData("/api/v1/support/tickets")]
    public async Task AllProtectedEndpoints_NoAuth_ShouldReturn401(string endpoint)
    {
        var res = await _f.CreateAnonymousClient().GetAsync(endpoint);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════════════════
    // CONTENT-TYPE BYPASS — JSON olmayan body'yi taramaz
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NonJsonContentType_ShouldNotBeScanned()
    {
        var admin = _f.CreateAdminClient();
        var payload = new StringContent(
            "<script>alert(1)</script>",
            Encoding.UTF8, "text/plain");
        // text/plain body taranmaz — bloklanmamalı
        var res = await admin.PostAsync("/api/v1/courses", payload);
        // Bloklanmamalı (content type uyumsuz)
        res.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }
}
