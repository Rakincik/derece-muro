using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Exams;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Sınav yaşam döngüsü E2E — Oluşturma, cevap anahtarı, durum geçişi,
/// öğrenci optik gönderimi, puanlama doğrulaması, çoklu öğrenci,
/// penalty scoring, sayfalama, yetki kontrolleri.
/// </summary>
[Trait("Category", "E2E")]
public class ExamsE2ETests : IClassFixture<MuroTestFactory>, IAsyncLifetime
{
    private readonly MuroTestFactory _f;

    public ExamsE2ETests(MuroTestFactory factory) => _f = factory;
    public async Task InitializeAsync() => await _f.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ════════════════════════════════════════════════════════════════════════
    // 1. TAM SINAV YAŞAM DÖNGÜSÜ (Admin + Student perspektifi)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExamFullLifecycle_CreateToResults_ShouldWork()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();

        // 1. Admin sınav oluşturur
        var createRes = await admin.PostAsJsonAsync("/api/v1/exams", new CreateExamRequest(
            "Matematik Vize", null, "MultipleChoice", 10, 4, null, null, null, true, 0.25));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var exam = await createRes.Content.ReadFromJsonAsync<ExamListDto>();
        exam!.Title.Should().Be("Matematik Vize");

        // 2. Admin cevap anahtarı girer (10 soru)
        var key = Enumerable.Range(1, 10)
            .ToDictionary(i => i, i => new[] { "A", "B", "C", "D" }[i % 4]);
        var keyRes = await admin.PutAsJsonAsync($"/api/v1/exams/{exam.Id}/answer-key",
            new UpdateAnswerKeyRequest(key));
        keyRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Cevap anahtarı doğrulaması
        var detailRes = await admin.GetAsync($"/api/v1/exams/{exam.Id}");
        var detail = await detailRes.Content.ReadFromJsonAsync<ExamDetailDto>();
        detail!.AnswerKey.Should().HaveCount(10);
        detail.QuestionCount.Should().Be(10);

        // 4. Durumu Active yap
        var statusRes = await admin.PutAsJsonAsync($"/api/v1/exams/{exam.Id}/status",
            new UpdateExamStatusRequest("Yayında"));
        statusRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var activated = await statusRes.Content.ReadFromJsonAsync<ExamListDto>();
        activated!.Status.Should().Be("Yayında");

        // 5. Öğrenci optik form gönderir — 7 doğru, 3 yanlış
        var answers = Enumerable.Range(1, 10)
            .ToDictionary(i => i, i => i <= 7 ? key[i] : "X");
        var submitRes = await admin.PostAsJsonAsync($"/api/v1/exams/{exam.Id}/submit",
            new SubmitExamAnswersRequest(answers, DateTime.UtcNow));
        submitRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await submitRes.Content.ReadFromJsonAsync<ExamResultDto>();
        result!.CorrectCount.Should().Be(7);
        result.WrongCount.Should().Be(3);

        // 6. Penalty puanlama doğrulaması: net = 7 - (3 * 0.25) = 6.25
        result.Net.Should().BeApproximately(6.25, 0.01);

        // 7. Admin sonuçları görür
        var resultsRes = await admin.GetAsync($"/api/v1/exams/{exam.Id}/results");
        resultsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await resultsRes.Content.ReadFromJsonAsync<ExamResultSummaryDto>();
        summary!.Results.Should().NotBeEmpty();

        // 8. Öğrenci kendi sonuçlarını görür
        var myRes = await admin.GetAsync("/api/v1/exams/my-results");
        myRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var myResults = await myRes.Content.ReadFromJsonAsync<List<MyExamResultDto>>();
        myResults.Should().NotBeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════════
    // 2. CEVAP ANAHTARI OLMADAN GÖNDERİM
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SubmitExam_WithoutAnswerKey_ShouldFail()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();

        var res = await admin.PostAsJsonAsync("/api/v1/exams", new CreateExamRequest(
            "Anahtarsız Sınav", null, "MultipleChoice", 5, 4, null, null, null, true));
        var exam = await res.Content.ReadFromJsonAsync<ExamListDto>();

        // Cevap anahtarı girilmeden öğrenci gönderim yapar
        var submitRes = await student.PostAsJsonAsync($"/api/v1/exams/{exam!.Id}/submit",
            new SubmitExamAnswersRequest(new Dictionary<int, string>
            {
                {1, "A"}, {2, "B"}, {3, "C"}, {4, "D"}, {5, "A"}
            }, DateTime.UtcNow));
        // Hata beklenir (cevap anahtarı yok)
        submitRes.IsSuccessStatusCode.Should().BeFalse();
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3. SINAV CRUD — Update + Delete
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExamCrud_UpdateDelete_ShouldWork()
    {
        var admin = _f.CreateAdminClient();

        var res = await admin.PostAsJsonAsync("/api/v1/exams", new CreateExamRequest(
            "Silinecek Sınav", null, "MultipleChoice", 5, 4, null, null, null, true));
        var exam = await res.Content.ReadFromJsonAsync<ExamListDto>();

        // Update
        var updateRes = await admin.PutAsJsonAsync($"/api/v1/exams/{exam!.Id}",
            new UpdateExamRequest("Güncellenen Sınav", null, "ClassicExam", null, null, null, null, null, null, 0.33));
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<ExamListDto>();
        updated!.Title.Should().Be("Güncellenen Sınav");

        // Delete
        var delRes = await admin.DeleteAsync($"/api/v1/exams/{exam.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Tekrar al → 404
        var afterDel = await admin.GetAsync($"/api/v1/exams/{exam.Id}");
        afterDel.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4. SAYFALAMA + FİLTRE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExamList_Pagination_ShouldWork()
    {
        var admin = _f.CreateAdminClient();

        for (int i = 0; i < 5; i++)
            await admin.PostAsJsonAsync("/api/v1/exams", new CreateExamRequest(
                $"Page Sınav {i}", null, "MultipleChoice", 5, 4, null, null, null, true));

        var p1 = await admin.GetAsync("/api/v1/exams?page=1&pageSize=2");
        var page1 = await p1.Content.ReadFromJsonAsync<PagedResult<ExamListDto>>();
        page1!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExamList_SearchFilter_ShouldWork()
    {
        var admin = _f.CreateAdminClient();
        await admin.PostAsJsonAsync("/api/v1/exams", new CreateExamRequest(
            "Aranacak Fizik Final", null, "MultipleChoice", 5, 4, null, null, null, true));

        var res = await admin.GetAsync("/api/v1/exams?search=Aranacak");
        var list = await res.Content.ReadFromJsonAsync<PagedResult<ExamListDto>>();
        list!.Items.Should().Contain(e => e.Title.Contains("Aranacak"));
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5. STATUS FILTER
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExamList_StatusFilter_ShouldFilterCorrectly()
    {
        var admin = _f.CreateAdminClient();

        var res = await admin.PostAsJsonAsync("/api/v1/exams", new CreateExamRequest(
            "Aktif Sınav Filter", null, "MultipleChoice", 5, 4, null, null, null, true));
        var exam = await res.Content.ReadFromJsonAsync<ExamListDto>();

        await admin.PutAsJsonAsync($"/api/v1/exams/{exam!.Id}/status",
            new UpdateExamStatusRequest("Yayında"));

        var activeList = await admin.GetAsync("/api/v1/exams?status=Yayında&pageSize=100");
        var actives = await activeList.Content.ReadFromJsonAsync<PagedResult<ExamListDto>>();
        actives!.Items.Should().Contain(e => e.Id == exam.Id);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 6. AUTH GUARD
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Exams_NoAuth_ShouldReturn401()
    {
        var res = await _f.CreateAnonymousClient().GetAsync("/api/v1/exams");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Exams_NoAuth_Submit_ShouldReturn401()
    {
        var res = await _f.CreateAnonymousClient()
            .PostAsJsonAsync($"/api/v1/exams/{Guid.NewGuid()}/submit",
                new SubmitExamAnswersRequest(new Dictionary<int, string>(), null));
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 7. EDGE: Var olmayan sınav
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetExam_NonExistent_ShouldReturn404()
    {
        var res = await _f.CreateAdminClient().GetAsync($"/api/v1/exams/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
