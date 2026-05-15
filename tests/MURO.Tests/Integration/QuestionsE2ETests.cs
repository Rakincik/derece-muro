using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Courses;
using MURO.Application.DTOs.Questions;
using MURO.Infrastructure.Persistence;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Soru-Cevap E2E — Çoklu soru yaşam döngüsü, cevaplama, not güncelleme,
/// status filtresi, sayfalama, instructor filtresi, yetki kontrolleri.
/// </summary>
[Trait("Category", "E2E")]
public class QuestionsE2ETests : IClassFixture<MuroTestFactory>, IAsyncLifetime
{
    private readonly MuroTestFactory _f;

    public QuestionsE2ETests(MuroTestFactory factory) => _f = factory;
    public async Task InitializeAsync() => await _f.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> EnsureCourseExists(HttpClient admin)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MuroDbContext>();
        var existing = await db.Courses.FirstOrDefaultAsync(c => c.TenantId == _f.TestTenantId);
        if (existing != null) return existing.Id;

        var res = await admin.PostAsJsonAsync("/api/v1/courses",
            new CreateCourseRequest("Soru Test Kursu", null, null, "Online", null, null));
        return (await res.Content.ReadFromJsonAsync<CourseListDto>())!.Id;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 1. TAM SORU YAŞAM DÖNGÜSÜ (Öğrenci sorar → Admin cevaplar → Not)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task QuestionLifecycle_AskAnswerNote_FullFlow()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();
        var courseId = await EnsureCourseExists(admin);

        // 1. Öğrenci soru sorar
        var askRes = await student.PostAsJsonAsync("/api/v1/questions",
            new CreateQuestionRequest(_f.TestAdminId, "İntegral ne işe yarar?",
                "İntegral ne işe yarar?", null, null, null, courseId));
        askRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var q = await askRes.Content.ReadFromJsonAsync<QuestionDto>();
        q!.Body.Should().Be("İntegral ne işe yarar?");
        q.Status.Should().Be("pending");

        // 2. Soru detayı
        var detailRes = await admin.GetAsync($"/api/v1/questions/{q.Id}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailRes.Content.ReadFromJsonAsync<QuestionDto>();
        detail!.Body.Should().Be("İntegral ne işe yarar?");

        // 3. Admin cevaplar → status "answered" olmalı
        var answerRes = await admin.PutAsJsonAsync($"/api/v1/questions/{q.Id}/answer",
            new AnswerQuestionRequest("İntegral, bir fonksiyonun altındaki alanı hesaplamak için kullanılır."));
        answerRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var answered = await answerRes.Content.ReadFromJsonAsync<QuestionDto>();
        answered!.Status.Should().Be("answered");
        answered.Answer.Should().Contain("alan");

        // 4. Öğrenci not günceller
        var noteRes = await student.PatchAsJsonAsync($"/api/v1/questions/{q.Id}/note",
            new UpdateNoteRequest("Sınavda çıkabilir, tekrar çalış"));
        noteRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var noted = await noteRes.Content.ReadFromJsonAsync<QuestionDto>();
        noted!.Note.Should().Contain("Sınavda");
    }

    // ════════════════════════════════════════════════════════════════════════
    // 2. ÇOKLU SORU + STATUS FİLTRESİ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task QuestionList_StatusFilter_ShouldFilterCorrectly()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();
        var courseId = await EnsureCourseExists(admin);

        // 3 soru oluştur
        for (int i = 1; i <= 3; i++)
        {
            await student.PostAsJsonAsync("/api/v1/questions",
                new CreateQuestionRequest(_f.TestAdminId, $"Filtre Soru {i}",
                    $"Filtre Soru {i}", null, null, null, courseId));
        }

        // İlk soruyu cevapla
        var allRes = await admin.GetAsync("/api/v1/questions?pageSize=100");
        var all = await allRes.Content.ReadFromJsonAsync<PagedResult<QuestionDto>>();
        var firstPending = all!.Items.FirstOrDefault(q => q.Status == "pending");
        if (firstPending != null)
        {
            await admin.PutAsJsonAsync($"/api/v1/questions/{firstPending.Id}/answer",
                new AnswerQuestionRequest("Cevap verildi."));
        }

        // Answered filtresi
        var answeredRes = await admin.GetAsync("/api/v1/questions?status=answered");
        var answeredList = await answeredRes.Content.ReadFromJsonAsync<PagedResult<QuestionDto>>();
        answeredList!.Items.Should().AllSatisfy(q => q.Status.Should().Be("answered"));

        // Pending filtresi
        var pendingRes = await admin.GetAsync("/api/v1/questions?status=pending");
        var pendingList = await pendingRes.Content.ReadFromJsonAsync<PagedResult<QuestionDto>>();
        pendingList!.Items.Should().AllSatisfy(q => q.Status.Should().Be("pending"));
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3. SAYFALAMA
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task QuestionList_Pagination_ShouldWork()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();
        var courseId = await EnsureCourseExists(admin);

        for (int i = 0; i < 5; i++)
            await student.PostAsJsonAsync("/api/v1/questions",
                new CreateQuestionRequest(_f.TestAdminId, $"Page Soru {i}", $"Page Soru {i}", null, null, null, courseId));

        var p1 = await admin.GetAsync("/api/v1/questions?page=1&pageSize=2");
        var page1 = await p1.Content.ReadFromJsonAsync<PagedResult<QuestionDto>>();
        page1!.Items.Should().HaveCount(2);
        page1.TotalCount.Should().BeGreaterThanOrEqualTo(5);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4. INSTRUCTOR FİLTRESİ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task QuestionList_InstructorFilter_ShouldWork()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();
        var courseId = await EnsureCourseExists(admin);

        await student.PostAsJsonAsync("/api/v1/questions",
            new CreateQuestionRequest(_f.TestAdminId, "Instructor Soru", "Instructor Soru", null, null, null, courseId));

        var res = await admin.GetAsync($"/api/v1/questions?instructorId={_f.TestAdminId}");
        var list = await res.Content.ReadFromJsonAsync<PagedResult<QuestionDto>>();
        list!.Items.Should().NotBeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5. AUTH
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Questions_NoAuth_ShouldReturn401()
    {
        (await _f.CreateAnonymousClient().GetAsync("/api/v1/questions"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Questions_NoAuth_Ask_ShouldReturn401()
    {
        (await _f.CreateAnonymousClient().PostAsJsonAsync("/api/v1/questions",
            new CreateQuestionRequest(Guid.NewGuid(), "x", "x", null, null, null, Guid.NewGuid())))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 6. EDGE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetQuestion_NonExistent_ShouldReturn404()
    {
        var res = await _f.CreateAdminClient().GetAsync($"/api/v1/questions/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
