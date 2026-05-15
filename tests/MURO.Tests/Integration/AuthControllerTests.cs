using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MURO.Application.DTOs.Auth;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Auth Controller integration testleri — Gerçek HTTP pipeline.
/// SessionValidationMiddleware, Testing ortamında bypass edilir.
/// </summary>
[Trait("Category", "Integration")]
public class AuthControllerTests : IClassFixture<MuroTestFactory>, IAsyncLifetime
{
    private readonly MuroTestFactory _factory;

    public AuthControllerTests(MuroTestFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── 1. Başarılı login → 200 + Token ─────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ShouldReturn200WithToken()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("admin@test.com", "Admin@1234"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be("admin@test.com");
    }

    // ── 2. Yanlış şifre → hata ─────────────────────────────────────────────

    [Fact]
    public async Task Login_WrongPassword_ShouldReturnError()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("admin@test.com", "WrongPassword!"));

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    // ── 3. Token yok → 401 ──────────────────────────────────────────────────

    [Fact]
    public async Task Me_NoToken_ShouldReturn401()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── 4. Geçerli admin token → 200 ────────────────────────────────────────

    [Fact]
    public async Task Me_ValidAdminToken_ShouldReturn200()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── 5. Geçersiz session → (bypass'ta 200, prod'da 401) ──────────────────

    [Fact]
    public async Task Me_InvalidSessionId_InTestEnv_ShouldBypassValidation()
    {
        var fakeSessionId = Guid.NewGuid(); // DB'de yok
        var token = _factory.GenerateToken(_factory.TestAdminId, "Admin", fakeSessionId, _factory.TestTenantId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", _factory.TestTenantId.ToString());

        var response = await client.GetAsync("/api/v1/auth/me");

        // Testing ortamında SessionValidation bypass → 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
