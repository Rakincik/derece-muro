using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Assignments;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// AssignmentService için unit testler — Ödev CRUD, öğrenci teslim,
/// süre dolmuş teslim reddi, çift teslim engeli, puanlama, öğrenci ödev listesi.
/// </summary>
public class AssignmentServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly AssignmentService _service;
    private readonly Mock<IGroupAccessService> _groupAccessMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public AssignmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _groupAccessMock = new Mock<IGroupAccessService>();
        _service = new AssignmentService(_db, _groupAccessMock.Object, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "hw" });
        _db.Users.Add(new User { Id = _userId, Email = "s@t.com", FirstName = "Ali", LastName = "Y", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Fizik 101" });
        _db.SaveChanges();
    }

    private CreateAssignmentRequest MakeAssignment(string title = "Ödev 1", DateTime? due = null) =>
        new(title, "Konu çalışma", _courseId, due ?? DateTime.UtcNow.AddDays(7), null, 100);

    // ════════════════════════════════════════════════════════════════════════
    // CREATE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateAssignment_ShouldSaveToDb()
    {
        var result = await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());

        result.Title.Should().Be("Ödev 1");
        result.CourseName.Should().Be("Fizik 101");
        result.SubmissionCount.Should().Be(0);
        (await _db.Assignments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAssignment_InvalidCourse_ShouldThrow()
    {
        var act = () => _service.CreateAssignmentAsync(_tenantId,
            new CreateAssignmentRequest("X", null, Guid.NewGuid(), DateTime.UtcNow.AddDays(7), null, 100));
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Ders bulunamadı*");
    }

    // ════════════════════════════════════════════════════════════════════════
    // GET BY ID
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetById_ShouldReturnWithSubmissions()
    {
        var created = await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());
        var detail = await _service.GetAssignmentByIdAsync(_tenantId, created.Id);

        detail.Title.Should().Be("Ödev 1");
        detail.MaxScore.Should().Be(100);
        detail.Submissions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_NonExistent_ShouldThrow()
    {
        var act = () => _service.GetAssignmentByIdAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Ödev bulunamadı*");
    }

    // ════════════════════════════════════════════════════════════════════════
    // UPDATE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateAssignment_ShouldModifyFields()
    {
        var created = await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());
        var updated = await _service.UpdateAssignmentAsync(_tenantId, created.Id,
            new UpdateAssignmentRequest("Güncel Ödev", "Yeni açıklama", null, null, 200));

        updated.Title.Should().Be("Güncel Ödev");
    }

    [Fact]
    public async Task UpdateAssignment_NonExistent_ShouldThrow()
    {
        var act = () => _service.UpdateAssignmentAsync(_tenantId, Guid.NewGuid(), new UpdateAssignmentRequest("X", null, null, null, null));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ════════════════════════════════════════════════════════════════════════
    // DELETE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteAssignment_ShouldRemove()
    {
        var created = await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());
        await _service.DeleteAssignmentAsync(_tenantId, created.Id);
        (await _db.Assignments.AnyAsync(a => a.Id == created.Id)).Should().BeFalse();
    }

    // ════════════════════════════════════════════════════════════════════════
    // SUBMIT
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Submit_ShouldCreateSubmission()
    {
        var created = await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());
        var result = await _service.SubmitAsync(_tenantId, created.Id, _userId,
            new SubmitAssignmentRequest("/uploads/odev.pdf", "İşte ödevim"));

        result.FileUrl.Should().Be("/uploads/odev.pdf");
        result.Comment.Should().Be("İşte ödevim");
        result.UserFullName.Should().Contain("Ali");
    }

    [Fact]
    public async Task Submit_Expired_ShouldThrow()
    {
        // Süre dolmuş ödev
        var created = await _service.CreateAssignmentAsync(_tenantId,
            MakeAssignment(due: DateTime.UtcNow.AddDays(-1)));

        var act = () => _service.SubmitAsync(_tenantId, created.Id, _userId,
            new SubmitAssignmentRequest(null, null));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*teslim süresi dolmuş*");
    }

    [Fact]
    public async Task Submit_Duplicate_ShouldThrow()
    {
        var created = await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());
        await _service.SubmitAsync(_tenantId, created.Id, _userId, new SubmitAssignmentRequest(null, "İlk"));

        var act = () => _service.SubmitAsync(_tenantId, created.Id, _userId, new SubmitAssignmentRequest(null, "İkinci"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*zaten bir teslim yapılmış*");
    }

    [Fact]
    public async Task Submit_NonExistentAssignment_ShouldThrow()
    {
        var act = () => _service.SubmitAsync(_tenantId, Guid.NewGuid(), _userId, new SubmitAssignmentRequest(null, null));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ════════════════════════════════════════════════════════════════════════
    // GRADE
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GradeSubmission_ShouldSetScoreAndFeedback()
    {
        var created = await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());
        var submission = await _service.SubmitAsync(_tenantId, created.Id, _userId, new SubmitAssignmentRequest(null, null));

        var graded = await _service.GradeSubmissionAsync(_tenantId, created.Id, submission.Id,
            new GradeSubmissionRequest(85, "İyi çalışma!"));

        graded.Score.Should().Be(85);
        graded.Feedback.Should().Be("İyi çalışma!");
    }

    [Fact]
    public async Task GradeSubmission_NonExistent_ShouldThrow()
    {
        var created = await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());
        var act = () => _service.GradeSubmissionAsync(_tenantId, created.Id, Guid.NewGuid(),
            new GradeSubmissionRequest(50, null));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ════════════════════════════════════════════════════════════════════════
    // GET MY ASSIGNMENTS (Öğrenci)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMyAssignments_Pending_ShouldShowPending()
    {
        await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());
        _groupAccessMock.Setup(g => g.GetAccessibleCourseIdsAsync(_tenantId, _userId))
            .ReturnsAsync(new HashSet<Guid> { _courseId });

        var myAssignments = await _service.GetMyAssignmentsAsync(_tenantId, _userId);

        myAssignments.Should().HaveCount(1);
        myAssignments[0].Status.Should().Be("pending");
        myAssignments[0].SubmissionId.Should().BeNull();
    }

    [Fact]
    public async Task GetMyAssignments_Submitted_ShouldShowSubmitted()
    {
        var created = await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());
        await _service.SubmitAsync(_tenantId, created.Id, _userId, new SubmitAssignmentRequest("/f.pdf", "Teslim"));

        _groupAccessMock.Setup(g => g.GetAccessibleCourseIdsAsync(_tenantId, _userId))
            .ReturnsAsync(new HashSet<Guid> { _courseId });

        var myAssignments = await _service.GetMyAssignmentsAsync(_tenantId, _userId);
        myAssignments[0].Status.Should().Be("submitted");
        myAssignments[0].SubmissionId.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMyAssignments_Graded_ShouldShowGraded()
    {
        var created = await _service.CreateAssignmentAsync(_tenantId, MakeAssignment());
        var sub = await _service.SubmitAsync(_tenantId, created.Id, _userId, new SubmitAssignmentRequest(null, null));
        await _service.GradeSubmissionAsync(_tenantId, created.Id, sub.Id, new GradeSubmissionRequest(90, "Harika"));

        _groupAccessMock.Setup(g => g.GetAccessibleCourseIdsAsync(_tenantId, _userId))
            .ReturnsAsync(new HashSet<Guid> { _courseId });

        var myAssignments = await _service.GetMyAssignmentsAsync(_tenantId, _userId);
        myAssignments[0].Status.Should().Be("graded");
        myAssignments[0].Score.Should().Be(90);
    }

    [Fact]
    public async Task GetMyAssignments_Overdue_ShouldShowOverdue()
    {
        await _service.CreateAssignmentAsync(_tenantId, MakeAssignment(due: DateTime.UtcNow.AddDays(-1)));

        _groupAccessMock.Setup(g => g.GetAccessibleCourseIdsAsync(_tenantId, _userId))
            .ReturnsAsync(new HashSet<Guid> { _courseId });

        var myAssignments = await _service.GetMyAssignmentsAsync(_tenantId, _userId);
        myAssignments[0].Status.Should().Be("overdue");
    }

    // ════════════════════════════════════════════════════════════════════════
    // LIST (Paging)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAssignments_Paging_ShouldWork()
    {
        for (int i = 0; i < 15; i++)
            await _service.CreateAssignmentAsync(_tenantId, MakeAssignment($"Ödev {i}"));

        var p1 = await _service.GetAssignmentsAsync(_tenantId, 1, 10, null);
        p1.Items.Should().HaveCount(10);
        p1.TotalCount.Should().Be(15);
    }

    public void Dispose() => _db.Dispose();
}
