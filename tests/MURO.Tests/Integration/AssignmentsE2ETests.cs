using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Assignments;
using MURO.Application.DTOs.Courses;
using MURO.Infrastructure.Persistence;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Ödev yaşam döngüsü E2E — CRUD, deadline enforcement, çift teslim engeli,
/// notlandırma, öğrenci perspektifi, sayfalama, yetki kontrolleri.
/// </summary>
[Trait("Category", "E2E")]
public class AssignmentsE2ETests : IClassFixture<MuroTestFactory>, IAsyncLifetime
{
    private readonly MuroTestFactory _f;

    public AssignmentsE2ETests(MuroTestFactory factory) => _f = factory;
    public async Task InitializeAsync() => await _f.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> EnsureCourseExists(HttpClient admin)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MuroDbContext>();
        var existing = await db.Courses.FirstOrDefaultAsync(c => c.TenantId == _f.TestTenantId);
        if (existing != null) return existing.Id;

        var res = await admin.PostAsJsonAsync("/api/v1/courses",
            new CreateCourseRequest("Ödev Test Kursu", null, null, "Online", null, null));
        var c = await res.Content.ReadFromJsonAsync<CourseListDto>();
        return c!.Id;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 1. TAM ÖDEV YAŞAM DÖNGÜSÜ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignmentLifecycle_CreateSubmitGradeView_FullFlow()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();
        var courseId = await EnsureCourseExists(admin);

        // 1. Admin ödev oluşturur (7 gün sonra deadline)
        var createRes = await admin.PostAsJsonAsync("/api/v1/assignments",
            new CreateAssignmentRequest("Kapsamlı Ödev 1",
                "Detaylı açıklama burada", courseId,
                DateTime.UtcNow.AddDays(7), null, 100));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var assignment = await createRes.Content.ReadFromJsonAsync<AssignmentListDto>();
        assignment!.Title.Should().Be("Kapsamlı Ödev 1");

        // 2. Admin ödev detayını kontrol eder
        var detailRes = await admin.GetAsync($"/api/v1/assignments/{assignment.Id}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailRes.Content.ReadFromJsonAsync<AssignmentDetailDto>();
        detail!.Description.Should().Be("Detaylı açıklama burada");

        // 3. Öğrenci ödevi teslim eder
        var submitRes = await student.PostAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}/submit",
            new SubmitAssignmentRequest("/uploads/teslim.pdf", "Teslim içeriğim burada. Lorem ipsum."));
        submitRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var submission = await submitRes.Content.ReadFromJsonAsync<SubmissionDto>();
        submission!.Comment.Should().Contain("Lorem ipsum");
        submission.Score.Should().BeNull(); // henüz notlanmamış

        // 4. ÇİFT TESLİM ENGELİ — aynı öğrenci tekrar teslim edemez
        var doubleRes = await student.PostAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}/submit",
            new SubmitAssignmentRequest(null, "İkinci deneme"));
        doubleRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 5. Admin notlandırır
        var gradeRes = await admin.PutAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}/submissions/{submission.Id}/grade",
            new GradeSubmissionRequest(92, "Harika bir çalışma, küçük eksikler var."));
        gradeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var graded = await gradeRes.Content.ReadFromJsonAsync<SubmissionDto>();
        graded!.Score.Should().Be(92);
        graded.Feedback.Should().Contain("Harika");

        // 6. Admin kendi ödevlerini görür (MyAssignments)
        var myRes = await admin.GetAsync("/api/v1/assignments/my");
        myRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var myAssignments = await myRes.Content.ReadFromJsonAsync<List<MyAssignmentDto>>();
        myAssignments.Should().NotBeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════════
    // 2. ÖDEV UPDATE + DELETE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignmentCrud_UpdateDelete_ShouldWork()
    {
        var admin = _f.CreateAdminClient();
        var courseId = await EnsureCourseExists(admin);

        var res = await admin.PostAsJsonAsync("/api/v1/assignments",
            new CreateAssignmentRequest("CRUD Ödev", null, courseId,
                DateTime.UtcNow.AddDays(5), null, 50));
        var a = await res.Content.ReadFromJsonAsync<AssignmentListDto>();

        // Update
        var updateRes = await admin.PutAsJsonAsync($"/api/v1/assignments/{a!.Id}",
            new UpdateAssignmentRequest("CRUD Ödev Güncel", "Yeni açıklama",
                DateTime.UtcNow.AddDays(10), null, 80));
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<AssignmentListDto>();
        updated!.Title.Should().Be("CRUD Ödev Güncel");

        // Delete
        var delRes = await admin.DeleteAsync($"/api/v1/assignments/{a.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 404 kontrolü
        var afterDel = await admin.GetAsync($"/api/v1/assignments/{a.Id}");
        afterDel.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3. SAYFALAMA + KURS FİLTRESİ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignmentList_PaginationAndFilter_ShouldWork()
    {
        var admin = _f.CreateAdminClient();
        var courseId = await EnsureCourseExists(admin);

        for (int i = 0; i < 5; i++)
            await admin.PostAsJsonAsync("/api/v1/assignments",
                new CreateAssignmentRequest($"Page Ödev {i}", null, courseId,
                    DateTime.UtcNow.AddDays(7), null, 100));

        // Sayfalama
        var p1 = await admin.GetAsync("/api/v1/assignments?page=1&pageSize=2");
        var page1 = await p1.Content.ReadFromJsonAsync<PagedResult<AssignmentListDto>>();
        page1!.Items.Should().HaveCount(2);

        // Kurs filtresi
        var filtered = await admin.GetAsync($"/api/v1/assignments?courseId={courseId}");
        var result = await filtered.Content.ReadFromJsonAsync<PagedResult<AssignmentListDto>>();
        result!.Items.Should().NotBeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4. AUTH GUARD
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assignments_NoAuth_ShouldReturn401()
    {
        var res = await _f.CreateAnonymousClient().GetAsync("/api/v1/assignments");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assignments_NoAuth_Submit_ShouldReturn401()
    {
        var res = await _f.CreateAnonymousClient()
            .PostAsJsonAsync($"/api/v1/assignments/{Guid.NewGuid()}/submit",
                new SubmitAssignmentRequest(null, "x"));
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5. EDGE CASES
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAssignment_NonExistent_ShouldReturn404()
    {
        var res = await _f.CreateAdminClient().GetAsync($"/api/v1/assignments/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
