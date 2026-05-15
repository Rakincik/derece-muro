using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Exams;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// ExamService için kapsamlı unit testler.
/// CRUD, cevap anahtarı, puan hesaplama, optik form gönderimi,
/// durum geçişleri, atama/atama kaldırma, öğrenci sonuç görüntüleme.
/// </summary>
public class ExamServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly ExamService _service;
    private readonly ExamAssignmentService _assignmentService;
    private readonly ExamResultService _resultService;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public ExamServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        var cacheMock = new MURO.Tests.Helpers.DummyCacheService();
        _service = new ExamService(_db, cacheMock);
        _assignmentService = new ExamAssignmentService(_db, cacheMock);
        _resultService = new ExamResultService(_db, cacheMock);

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "exam", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User
        {
            Id = _userId, Email = "student@test.com", FirstName = "Ali", LastName = "Yılmaz",
            PasswordHash = "h", Role = UserRole.Student, IsActive = true
        });
        _db.SaveChanges();
    }

    private CreateExamRequest MakeExam(string title = "TYT Deneme 01", int qCount = 40, int optCount = 5,
        double penalty = 0.25) =>
        new(title, "Matematik denemesi", "TYT", qCount, optCount, 120, null, null, true, penalty);

    // ═════════════════════════════════════════════════════════════════════════
    // CREATE
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateExam_ShouldSaveToDb()
    {
        var result = await _service.CreateExamAsync(_tenantId, MakeExam());

        result.Title.Should().Be("TYT Deneme 01");
        result.QuestionCount.Should().Be(40);
        result.Status.Should().Be("Taslak");
        (await _db.Exams.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateExam_ShouldStartAsTaslak()
    {
        var result = await _service.CreateExamAsync(_tenantId, MakeExam());
        result.Status.Should().Be("Taslak");
    }

    [Fact]
    public async Task CreateExam_WithCustomPenalty_ShouldSetCorrectly()
    {
        var result = await _service.CreateExamAsync(_tenantId, MakeExam(penalty: 0.33));
        result.WrongPenaltyWeight.Should().Be(0.33);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GET BY ID
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetExamById_ShouldReturn()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var detail = await _service.GetExamByIdAsync(_tenantId, created.Id);

        detail.Title.Should().Be("TYT Deneme 01");
        detail.QuestionCount.Should().Be(40);
    }

    [Fact]
    public async Task GetExamById_NonExistent_ShouldThrow()
    {
        var act = () => _service.GetExamByIdAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Sınav bulunamadı*");
    }

    [Fact]
    public async Task GetExamById_WrongTenant_ShouldThrow()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var act = () => _service.GetExamByIdAsync(Guid.NewGuid(), created.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UPDATE
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateExam_ShouldModifyFields()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var updated = await _service.UpdateExamAsync(_tenantId, created.Id,
            new UpdateExamRequest("Güncel Başlık", null, null, 80, null, 180, null, null, null, null));

        updated.Title.Should().Be("Güncel Başlık");
        updated.QuestionCount.Should().Be(80);
    }

    [Fact]
    public async Task UpdateExam_NonExistent_ShouldThrow()
    {
        var act = () => _service.UpdateExamAsync(_tenantId, Guid.NewGuid(),
            new UpdateExamRequest("X", null, null, null, null, null, null, null, null, null));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DELETE
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteExam_ShouldRemove()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        await _service.DeleteExamAsync(_tenantId, created.Id);
        (await _db.Exams.AnyAsync(e => e.Id == created.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteExam_NonExistent_ShouldThrow()
    {
        var act = () => _service.DeleteExamAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ANSWER KEY
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateAnswerKey_ShouldPersistAsJson()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam(qCount: 5));
        var key = new Dictionary<int, string> { { 1, "A" }, { 2, "B" }, { 3, "C" }, { 4, "D" }, { 5, "E" } };

        var detail = await _service.UpdateAnswerKeyAsync(_tenantId, created.Id, new UpdateAnswerKeyRequest(key));

        detail.AnswerKey.Should().NotBeNull();
        detail.AnswerKey!.Count.Should().Be(5);
        detail.AnswerKey[1].Should().Be("A");
        detail.AnswerKey[5].Should().Be("E");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // STATUS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStatus_ValidTransition_ShouldUpdate()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var result = await _service.UpdateStatusAsync(_tenantId, created.Id, new UpdateExamStatusRequest("Yayında"));
        result.Status.Should().Be("Yayında");
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_ShouldThrow()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var act = () => _service.UpdateStatusAsync(_tenantId, created.Id, new UpdateExamStatusRequest("Silinmiş"));
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Geçersiz durum*");
    }

    [Theory]
    [InlineData("Taslak")]
    [InlineData("Yayında")]
    [InlineData("Tamamlandı")]
    public async Task UpdateStatus_AllValidStatuses_ShouldSucceed(string status)
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var result = await _service.UpdateStatusAsync(_tenantId, created.Id, new UpdateExamStatusRequest(status));
        result.Status.Should().Be(status);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PDF
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdatePdf_ShouldSetUrls()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var detail = await _service.UpdatePdfAsync(_tenantId, created.Id,
            new UpdateExamPdfRequest("/pdf/exam.pdf", "/pdf/solution.pdf"));

        detail.PdfUrl.Should().Be("/pdf/exam.pdf");
        detail.SolutionPdfUrl.Should().Be("/pdf/solution.pdf");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ASSIGN / REMOVE ASSIGNMENT
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignExam_ToUser_ShouldCreateAssignment()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var assignment = await _assignmentService.AssignExamAsync(_tenantId, created.Id,
            new CreateExamAssignmentRequest("User", _userId, null, null));

        assignment.TargetType.Should().Be("User");
        assignment.TargetId.Should().Be(_userId);
        assignment.TargetName.Should().Contain("Ali");
    }

    [Fact]
    public async Task AssignExam_ToGroup_ShouldCreateAssignment()
    {
        var groupId = Guid.NewGuid();
        _db.Groups.Add(new Group { Id = groupId, TenantId = _tenantId, Name = "Fizik A" });
        await _db.SaveChangesAsync();

        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var assignment = await _assignmentService.AssignExamAsync(_tenantId, created.Id,
            new CreateExamAssignmentRequest("Group", groupId, null, null));

        assignment.TargetName.Should().Be("Fizik A");
    }

    [Fact]
    public async Task AssignExam_NonExistentExam_ShouldThrow()
    {
        var act = () => _assignmentService.AssignExamAsync(_tenantId, Guid.NewGuid(),
            new CreateExamAssignmentRequest("User", _userId, null, null));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RemoveAssignment_ShouldDelete()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var assignment = await _assignmentService.AssignExamAsync(_tenantId, created.Id,
            new CreateExamAssignmentRequest("User", _userId, null, null));

        await _assignmentService.RemoveAssignmentAsync(_tenantId, created.Id, assignment.Id);
        (await _db.ExamAssignments.AnyAsync(a => a.Id == assignment.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAssignment_NonExistent_ShouldThrow()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var act = () => _assignmentService.RemoveAssignmentAsync(_tenantId, created.Id, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Atama bulunamadı*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SUBMIT ANSWERS — Puan Hesaplama
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SubmitAnswers_AllCorrect_ShouldGet100()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam(qCount: 5));
        var key = new Dictionary<int, string> { { 1, "A" }, { 2, "B" }, { 3, "C" }, { 4, "D" }, { 5, "E" } };
        await _service.UpdateAnswerKeyAsync(_tenantId, created.Id, new UpdateAnswerKeyRequest(key));

        var answers = new Dictionary<int, string> { { 1, "A" }, { 2, "B" }, { 3, "C" }, { 4, "D" }, { 5, "E" } };
        var result = await _resultService.SubmitAnswersAsync(_tenantId, created.Id, _userId,
            new SubmitExamAnswersRequest(answers, null));

        result.CorrectCount.Should().Be(5);
        result.WrongCount.Should().Be(0);
        result.EmptyCount.Should().Be(0);
        result.Score.Should().Be(100);
    }

    [Fact]
    public async Task SubmitAnswers_AllWrong_ShouldApplyPenalty()
    {
        // 5 soru, 4 yanlış 1 doğru sistemi (0.25 penalty)
        var created = await _service.CreateExamAsync(_tenantId, MakeExam(qCount: 4, penalty: 0.25));
        var key = new Dictionary<int, string> { { 1, "A" }, { 2, "B" }, { 3, "C" }, { 4, "D" } };
        await _service.UpdateAnswerKeyAsync(_tenantId, created.Id, new UpdateAnswerKeyRequest(key));

        // Hepsi yanlış
        var answers = new Dictionary<int, string> { { 1, "B" }, { 2, "C" }, { 3, "D" }, { 4, "A" } };
        var result = await _resultService.SubmitAnswersAsync(_tenantId, created.Id, _userId,
            new SubmitExamAnswersRequest(answers, null));

        result.CorrectCount.Should().Be(0);
        result.WrongCount.Should().Be(4);
        // Net: 0 - (4 * 0.25) = -1.0
        result.Net.Should().Be(-1.0);
    }

    [Fact]
    public async Task SubmitAnswers_WithEmpty_ShouldCountCorrectly()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam(qCount: 5));
        var key = new Dictionary<int, string> { { 1, "A" }, { 2, "B" }, { 3, "C" }, { 4, "D" }, { 5, "E" } };
        await _service.UpdateAnswerKeyAsync(_tenantId, created.Id, new UpdateAnswerKeyRequest(key));

        // 3 doğru, 1 yanlış, 1 boş
        var answers = new Dictionary<int, string> { { 1, "A" }, { 2, "B" }, { 3, "C" }, { 4, "X" } };
        var result = await _resultService.SubmitAnswersAsync(_tenantId, created.Id, _userId,
            new SubmitExamAnswersRequest(answers, null));

        result.CorrectCount.Should().Be(3);
        result.WrongCount.Should().Be(1);
        result.EmptyCount.Should().Be(1);
    }

    [Fact]
    public async Task SubmitAnswers_WithoutAnswerKey_ShouldThrow()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam(qCount: 5));

        var answers = new Dictionary<int, string> { { 1, "A" } };
        var act = () => _resultService.SubmitAnswersAsync(_tenantId, created.Id, _userId,
            new SubmitExamAnswersRequest(answers, null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cevap anahtarı henüz girilmemiş*");
    }

    [Fact]
    public async Task SubmitAnswers_NetAndScoreCalculation_ShouldBeCorrect()
    {
        // 10 soru, 0.25 penalty
        var created = await _service.CreateExamAsync(_tenantId, MakeExam(qCount: 10, penalty: 0.25));
        var key = Enumerable.Range(1, 10).ToDictionary(i => i, i => ((char)('A' + i % 5)).ToString());
        await _service.UpdateAnswerKeyAsync(_tenantId, created.Id, new UpdateAnswerKeyRequest(key));

        // 7 doğru, 2 yanlış, 1 boş
        var answers = new Dictionary<int, string>();
        for (int i = 1; i <= 10; i++)
        {
            if (i <= 7) answers[i] = key[i]; // doğru
            else if (i <= 9) answers[i] = "X"; // yanlış
            // 10 boş
        }

        var result = await _resultService.SubmitAnswersAsync(_tenantId, created.Id, _userId,
            new SubmitExamAnswersRequest(answers, null));

        result.CorrectCount.Should().Be(7);
        result.WrongCount.Should().Be(2);
        result.EmptyCount.Should().Be(1);
        // Net: 7 - (2 * 0.25) = 6.5
        result.Net.Should().Be(6.5);
        // Score: (6.5 / 10) * 100 = 65
        result.Score.Should().Be(65);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // RESULTS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetExamResults_NoSubmissions_ShouldReturnEmptySummary()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam());
        var summary = await _resultService.GetExamResultsAsync(_tenantId, created.Id);

        summary.TotalParticipants.Should().Be(0);
        summary.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExamResults_WithSubmissions_ShouldCalculateStats()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam(qCount: 4));
        var key = new Dictionary<int, string> { { 1, "A" }, { 2, "B" }, { 3, "C" }, { 4, "D" } };
        await _service.UpdateAnswerKeyAsync(_tenantId, created.Id, new UpdateAnswerKeyRequest(key));

        // Öğrenci 1: Hepsi doğru
        await _resultService.SubmitAnswersAsync(_tenantId, created.Id, _userId,
            new SubmitExamAnswersRequest(key, null));

        // Öğrenci 2: 2 doğru 2 yanlış
        var user2Id = Guid.NewGuid();
        _db.Users.Add(new User { Id = user2Id, Email = "s2@t.com", FirstName = "Ayşe", LastName = "K", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        await _db.SaveChangesAsync();

        var answers2 = new Dictionary<int, string> { { 1, "A" }, { 2, "B" }, { 3, "X" }, { 4, "X" } };
        await _resultService.SubmitAnswersAsync(_tenantId, created.Id, user2Id,
            new SubmitExamAnswersRequest(answers2, null));

        var summary = await _resultService.GetExamResultsAsync(_tenantId, created.Id);

        summary.TotalParticipants.Should().Be(2);
        summary.HighestScore.Should().Be(100);
        summary.LowestScore.Should().BeLessThan(100);
        summary.Results.Should().HaveCount(2);
        // En yüksek puan ilk sırada olmalı (OrderByDescending Score)
        summary.Results[0].Score.Should().BeGreaterThanOrEqualTo(summary.Results[1].Score);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MY EXAM RESULTS (Öğrenci)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMyExamResults_ShouldReturnOnlyMyResults()
    {
        var created = await _service.CreateExamAsync(_tenantId, MakeExam(qCount: 4));
        var key = new Dictionary<int, string> { { 1, "A" }, { 2, "B" }, { 3, "C" }, { 4, "D" } };
        await _service.UpdateAnswerKeyAsync(_tenantId, created.Id, new UpdateAnswerKeyRequest(key));

        await _resultService.SubmitAnswersAsync(_tenantId, created.Id, _userId,
            new SubmitExamAnswersRequest(key, null));

        var myResults = await _resultService.GetMyExamResultsAsync(_tenantId, _userId);
        myResults.Should().HaveCount(1);
        myResults[0].ExamTitle.Should().Be("TYT Deneme 01");
        myResults[0].ShowResults.Should().BeTrue();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LIST (Paging / Search / Filter)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetExams_Paging_ShouldWork()
    {
        for (int i = 0; i < 25; i++)
            await _service.CreateExamAsync(_tenantId, MakeExam($"Sınav {i:D2}"));

        var p1 = await _service.GetExamsAsync(_tenantId, 1, 10, null, null, null);
        var p3 = await _service.GetExamsAsync(_tenantId, 3, 10, null, null, null);

        p1.Items.Should().HaveCount(10);
        p3.Items.Should().HaveCount(5);
        p1.TotalCount.Should().Be(25);
    }

    [Fact]
    public async Task GetExams_SearchByTitle_ShouldFilter()
    {
        await _service.CreateExamAsync(_tenantId, MakeExam("Matematik Deneme"));
        await _service.CreateExamAsync(_tenantId, MakeExam("Fizik Deneme"));
        await _service.CreateExamAsync(_tenantId, MakeExam("Türkçe Deneme"));

        var result = await _service.GetExamsAsync(_tenantId, 1, 10, "Fizik", null, null);
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Fizik Deneme");
    }

    [Fact]
    public async Task GetExams_FilterByStatus_ShouldFilter()
    {
        var e1 = await _service.CreateExamAsync(_tenantId, MakeExam("Taslak Sınav"));
        var e2 = await _service.CreateExamAsync(_tenantId, MakeExam("Yayın Sınav"));
        await _service.UpdateStatusAsync(_tenantId, e2.Id, new UpdateExamStatusRequest("Yayında"));

        var result = await _service.GetExamsAsync(_tenantId, 1, 10, null, null, "Yayında");
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Yayın Sınav");
    }

    public void Dispose() => _db.Dispose();
}
