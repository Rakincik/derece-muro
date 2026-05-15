using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Questions;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// QuestionService (Soru Sor) için unit testler — Soru sorma, cevaplama,
/// not güncelleme, durum geçişi, sayfalama, filtreleme.
/// </summary>
public class QuestionServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly QuestionService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _instructorId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();

    public QuestionServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _service = new QuestionService(_db, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "q" });
        _db.Users.Add(new User { Id = _studentId, Email = "s@t.com", FirstName = "Ali", LastName = "Öğrenci", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Users.Add(new User { Id = _instructorId, Email = "i@t.com", FirstName = "Hoca", LastName = "Bey", PasswordHash = "h", Role = UserRole.Admin, IsActive = true });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Fizik 101" });
        _db.SaveChanges();
    }

    private CreateQuestionRequest MakeQ(string subject = "Neden böyle?") =>
        new(_instructorId, subject, "Anlamadım hocam", null, null, null, _courseId);

    // ────────────────────────────────────────────────────────────────────────
    // ASK (Soru Sor)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ask_ShouldCreateQuestion()
    {
        var result = await _service.AskAsync(_tenantId, _studentId, MakeQ());

        result.Subject.Should().Be("Neden böyle?");
        result.Status.Should().Be("pending");
        result.UserFullName.Should().Contain("Ali");
        result.InstructorFullName.Should().Contain("Hoca");
        (await _db.Questions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Ask_WithCourse_ShouldSetCourseTitle()
    {
        var result = await _service.AskAsync(_tenantId, _studentId, MakeQ());
        result.CourseTitle.Should().Be("Fizik 101");
    }

    [Fact]
    public async Task Ask_WithMedia_ShouldSaveUrls()
    {
        var result = await _service.AskAsync(_tenantId, _studentId,
            new CreateQuestionRequest(_instructorId, "Soru", "Body", "/img/q.jpg", "/audio/q.mp3", "Özel not", null));

        result.ImageUrl.Should().Be("/img/q.jpg");
        result.AudioUrl.Should().Be("/audio/q.mp3");
        result.Note.Should().Be("Özel not");
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET BY ID
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ShouldReturn()
    {
        var asked = await _service.AskAsync(_tenantId, _studentId, MakeQ());
        var result = await _service.GetByIdAsync(_tenantId, asked.Id);
        result.Subject.Should().Be("Neden böyle?");
    }

    [Fact]
    public async Task GetById_NonExistent_ShouldThrow()
    {
        var act = () => _service.GetByIdAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Soru bulunamadı*");
    }

    [Fact]
    public async Task GetById_WrongTenant_ShouldThrow()
    {
        var asked = await _service.AskAsync(_tenantId, _studentId, MakeQ());
        var act = () => _service.GetByIdAsync(Guid.NewGuid(), asked.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // ANSWER (Cevapla)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Answer_ShouldSetAnswerAndStatus()
    {
        var asked = await _service.AskAsync(_tenantId, _studentId, MakeQ());
        var result = await _service.AnswerAsync(_tenantId, asked.Id, new AnswerQuestionRequest("Çünkü böyle."));

        result.Answer.Should().Be("Çünkü böyle.");
        result.Status.Should().Be("answered");
        result.AnsweredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Answer_NonExistent_ShouldThrow()
    {
        var act = () => _service.AnswerAsync(_tenantId, Guid.NewGuid(), new AnswerQuestionRequest("X"));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // UPDATE NOTE
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateNote_ShouldSetNote()
    {
        var asked = await _service.AskAsync(_tenantId, _studentId, MakeQ());
        var result = await _service.UpdateNoteAsync(_tenantId, asked.Id, _studentId, new UpdateNoteRequest("Yeni notum"));
        result.Note.Should().Be("Yeni notum");
    }

    [Fact]
    public async Task UpdateNote_WrongUser_ShouldThrow()
    {
        var asked = await _service.AskAsync(_tenantId, _studentId, MakeQ());
        var act = () => _service.UpdateNoteAsync(_tenantId, asked.Id, Guid.NewGuid(), new UpdateNoteRequest("Hack"));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // LIST (Paging, Status Filter, Instructor Filter)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuestions_Paging_ShouldWork()
    {
        for (int i = 0; i < 15; i++)
            await _service.AskAsync(_tenantId, _studentId, MakeQ($"Soru {i}"));

        var p1 = await _service.GetQuestionsAsync(_tenantId, 1, 10, null, null);
        var p2 = await _service.GetQuestionsAsync(_tenantId, 2, 10, null, null);

        p1.Items.Should().HaveCount(10);
        p2.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetQuestions_FilterByStatus_ShouldFilter()
    {
        var q1 = await _service.AskAsync(_tenantId, _studentId, MakeQ("Soru 1"));
        await _service.AskAsync(_tenantId, _studentId, MakeQ("Soru 2"));
        await _service.AnswerAsync(_tenantId, q1.Id, new AnswerQuestionRequest("Cevap"));

        var pending = await _service.GetQuestionsAsync(_tenantId, 1, 10, "pending", null);
        pending.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetQuestions_FilterByInstructor_ShouldFilter()
    {
        var otherInstructorId = Guid.NewGuid();
        _db.Users.Add(new User { Id = otherInstructorId, Email = "h2@t.com", FirstName = "Diğer", LastName = "Hoca", PasswordHash = "h", Role = UserRole.Admin, IsActive = true });
        await _db.SaveChangesAsync();

        await _service.AskAsync(_tenantId, _studentId, MakeQ("Fizik sorusu"));
        await _service.AskAsync(_tenantId, _studentId, new CreateQuestionRequest(otherInstructorId, "Mat sorusu", "...", null, null, null, null));

        var result = await _service.GetQuestionsAsync(_tenantId, 1, 10, null, _instructorId);
        result.Items.Should().HaveCount(1);
        result.Items[0].Subject.Should().Be("Fizik sorusu");
    }

    public void Dispose() => _db.Dispose();
}
