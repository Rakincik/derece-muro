using System.Net;
using FluentAssertions;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Courses Controller integration testleri — Rol bazlı erişim kontrolü.
/// SessionValidationMiddleware, Testing ortamında bypass edilir.
/// </summary>
[Trait("Category", "Integration")]
public class CoursesControllerTests : IClassFixture<MuroTestFactory>, IAsyncLifetime
{
    private readonly MuroTestFactory _factory;

    public CoursesControllerTests(MuroTestFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── 1. Admin token → kurslar gelir ──────────────────────────────────────

    [Fact]
    public async Task GetCourses_AdminToken_ShouldNotReturn401()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/courses");

        // Auth geçti → 401 veya 403 olmamalı
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // ── 2. Token yok → 401 ──────────────────────────────────────────────────

    [Fact]
    public async Task GetCourses_NoToken_ShouldReturn401()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/v1/courses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── 3. Student → Session Start 403 ──────────────────────────────────────

    [Fact]
    public async Task StartSession_StudentToken_ShouldReturn403()
    {
        var client = _factory.CreateStudentClient();

        var response = await client.PostAsync(
            $"/api/v1/courses/{Guid.NewGuid()}/sessions/{Guid.NewGuid()}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── 4. Student → Session End 403 ────────────────────────────────────────

    [Fact]
    public async Task EndSession_StudentToken_ShouldReturn403()
    {
        var client = _factory.CreateStudentClient();

        var response = await client.PostAsync(
            $"/api/v1/courses/{Guid.NewGuid()}/sessions/{Guid.NewGuid()}/end", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
