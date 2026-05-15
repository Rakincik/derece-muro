using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Courses;
using MURO.Infrastructure.Persistence;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Kurs yaşam döngüsü E2E — CRUD, Session yönetimi, sayfalama, arama,
/// yayınlama akışı, yetki kontrolleri, çapraz rol senaryoları.
/// </summary>
[Trait("Category", "E2E")]
public class CoursesE2ETests : IClassFixture<MuroTestFactory>, IAsyncLifetime
{
    private readonly MuroTestFactory _f;

    public CoursesE2ETests(MuroTestFactory factory) => _f = factory;
    public async Task InitializeAsync() => await _f.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ════════════════════════════════════════════════════════════════════════
    // 1. TAM YAŞAM DÖNGÜSÜ: Create → Get → Update → Delete → 404
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CourseLifecycle_CreateGetUpdateDelete_FullFlow()
    {
        var admin = _f.CreateAdminClient();

        // Create
        var createRes = await admin.PostAsJsonAsync("/api/v1/courses",
            new CreateCourseRequest("Fizik 101", "Temel fizik", null, "Online", null, null));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var course = await createRes.Content.ReadFromJsonAsync<CourseListDto>();
        course!.Title.Should().Be("Fizik 101");
        course.Description.Should().Be("Temel fizik");

        // Get by ID — detay doğrulaması
        var getRes = await admin.GetAsync($"/api/v1/courses/{course.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await getRes.Content.ReadFromJsonAsync<CourseDetailDto>();
        detail!.Title.Should().Be("Fizik 101");
        detail.Id.Should().Be(course.Id);

        // Update — tüm alanlar değişmeli
        var updateRes = await admin.PutAsJsonAsync($"/api/v1/courses/{course.Id}",
            new UpdateCourseRequest("Fizik 201", "İleri seviye fizik", null, null, null, null, null));
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<CourseListDto>();
        updated!.Title.Should().Be("Fizik 201");
        updated.Description.Should().Be("İleri seviye fizik");

        // Delete
        var delRes = await admin.DeleteAsync($"/api/v1/courses/{course.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Silinmiş kurs → 404
        var afterDel = await admin.GetAsync($"/api/v1/courses/{course.Id}");
        afterDel.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 2. SAYFALAMA + ARAMA
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CourseList_Pagination_ShouldReturnCorrectPages()
    {
        var admin = _f.CreateAdminClient();

        // 5 kurs oluştur
        for (int i = 1; i <= 5; i++)
            await admin.PostAsJsonAsync("/api/v1/courses",
                new CreateCourseRequest($"Paginate Kurs {i}", null, null, "Online", null, null));

        // Sayfa 1 (2'li sayfa)
        var p1 = await admin.GetAsync("/api/v1/courses?page=1&pageSize=2");
        p1.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await p1.Content.ReadFromJsonAsync<PagedResult<CourseListDto>>();
        page1!.Items.Should().HaveCount(2);
        page1.TotalCount.Should().BeGreaterThanOrEqualTo(5);

        // Sayfa 2
        var p2 = await admin.GetAsync("/api/v1/courses?page=2&pageSize=2");
        var page2 = await p2.Content.ReadFromJsonAsync<PagedResult<CourseListDto>>();
        page2!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CourseList_Search_ShouldFilterByTitle()
    {
        var admin = _f.CreateAdminClient();
        await admin.PostAsJsonAsync("/api/v1/courses",
            new CreateCourseRequest("Matematik Dersi Aranan", null, null, "Online", null, null));
        await admin.PostAsJsonAsync("/api/v1/courses",
            new CreateCourseRequest("Biyoloji Ders", null, null, "Online", null, null));

        var res = await admin.GetAsync("/api/v1/courses?search=Matematik");
        var result = await res.Content.ReadFromJsonAsync<PagedResult<CourseListDto>>();
        result!.Items.Should().AllSatisfy(c => c.Title.Should().Contain("Matematik"));
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3. SESSION CRUD İÇ İÇE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SessionLifecycle_CreateUpdateDelete_InsideCourse()
    {
        var admin = _f.CreateAdminClient();

        // Kurs oluştur
        var courseRes = await admin.PostAsJsonAsync("/api/v1/courses",
            new CreateCourseRequest("Session Kurs", null, null, "Online", null, null));
        var course = await courseRes.Content.ReadFromJsonAsync<CourseListDto>();

        // Session oluştur
        var sessionRes = await admin.PostAsJsonAsync($"/api/v1/courses/{course!.Id}/sessions",
            new CreateSessionRequest("Canlı Ders 1", null, null, null, null, false,
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), true));
        sessionRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await sessionRes.Content.ReadFromJsonAsync<SessionDto>();
        session!.Title.Should().Be("Canlı Ders 1");

        // Session güncelle
        var updateRes = await admin.PutAsJsonAsync(
            $"/api/v1/courses/{course.Id}/sessions/{session.Id}",
            new UpdateSessionRequest("Canlı Ders 1 (Güncel)", null, null, null, null, null,
                DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2), null));
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Kurs detayında session listesi
        var detailRes = await admin.GetAsync($"/api/v1/courses/{course.Id}");
        var detail = await detailRes.Content.ReadFromJsonAsync<CourseDetailDto>();
        detail!.Sessions.Should().NotBeEmpty();

        // Session sil
        var delRes = await admin.DeleteAsync($"/api/v1/courses/{course.Id}/sessions/{session.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4. ÇOKLU KURS + SİLME SONRASI LİSTE TUTARLILIĞI
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MultipleCoursesCreateDelete_ListShouldBeConsistent()
    {
        var admin = _f.CreateAdminClient();

        var ids = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var r = await admin.PostAsJsonAsync("/api/v1/courses",
                new CreateCourseRequest($"Tutarlılık Kurs {i}", null, null, "Online", null, null));
            var c = await r.Content.ReadFromJsonAsync<CourseListDto>();
            ids.Add(c!.Id);
        }

        // Ortadakini sil
        await admin.DeleteAsync($"/api/v1/courses/{ids[1]}");

        // Liste kontrolü — silinen olmamalı
        var listRes = await admin.GetAsync("/api/v1/courses?pageSize=100");
        var list = await listRes.Content.ReadFromJsonAsync<PagedResult<CourseListDto>>();
        list!.Items.Should().NotContain(c => c.Id == ids[1]);
        list.Items.Should().Contain(c => c.Id == ids[0]);
        list.Items.Should().Contain(c => c.Id == ids[2]);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5. AUTH GUARD — Farklı roller
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Courses_NoAuth_ShouldReturn401()
    {
        var res = await _f.CreateAnonymousClient().GetAsync("/api/v1/courses");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Courses_StudentCanList_ShouldReturn200()
    {
        var res = await _f.CreateStudentClient().GetAsync("/api/v1/courses");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Courses_StudentWithoutAccess_ShouldReturn403()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();

        var r = await admin.PostAsJsonAsync("/api/v1/courses",
            new CreateCourseRequest("Student Detay Kurs", null, null, "Online", null, null));
        var c = await r.Content.ReadFromJsonAsync<CourseListDto>();

        var res = await student.GetAsync($"/api/v1/courses/{c!.Id}");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 6. EDGE CASES
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCourse_NonExistent_ShouldReturn404()
    {
        var res = await _f.CreateAdminClient().GetAsync($"/api/v1/courses/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCourse_NonExistent_ShouldReturn404()
    {
        var res = await _f.CreateAdminClient().DeleteAsync($"/api/v1/courses/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCourse_Twice_SecondShouldReturn404()
    {
        var admin = _f.CreateAdminClient();
        var r = await admin.PostAsJsonAsync("/api/v1/courses",
            new CreateCourseRequest("Çift Sil", null, null, "Online", null, null));
        var c = await r.Content.ReadFromJsonAsync<CourseListDto>();

        (await admin.DeleteAsync($"/api/v1/courses/{c!.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/v1/courses/{c.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }
}
