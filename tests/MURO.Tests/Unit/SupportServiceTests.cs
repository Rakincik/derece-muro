using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Support;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// SupportService için unit testler — Destek talepleri ve SSS (FAQ) yönetimi.
/// Ticket CRUD, reply, close, durum geçişleri, FAQ sıralama.
/// </summary>
public class SupportServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly SupportService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    public SupportServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _service = new SupportService(_db, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "sup", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User
        {
            Id = _userId, Email = "student@t.com", FirstName = "Ali", LastName = "Yılmaz",
            PasswordHash = "h", Role = UserRole.Student, IsActive = true
        });
        _db.Users.Add(new User
        {
            Id = _adminId, Email = "admin@t.com", FirstName = "Hoca", LastName = "Bey",
            PasswordHash = "h", Role = UserRole.Admin, IsActive = true
        });
        _db.SaveChanges();
    }

    private CreateTicketRequest Ticket(string subject = "Login sorunu") =>
        new(subject, "Giriş yapamıyorum", "normal", "technical");

    // ────────────────────────────────────────────────────────────────────────
    // TICKET CRUD
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTicket_ShouldSaveToDb()
    {
        var result = await _service.CreateTicketAsync(_tenantId, _userId, Ticket());

        result.Subject.Should().Be("Login sorunu");
        result.Status.Should().Be("Open");
        result.UserFullName.Should().Contain("Ali");
        (await _db.SupportTickets.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateTicket_ShouldStartAsOpen()
    {
        var result = await _service.CreateTicketAsync(_tenantId, _userId, Ticket());
        result.Status.Should().Be("Open");
    }

    [Fact]
    public async Task GetTicketById_ShouldReturnWithMessages()
    {
        var ticket = await _service.CreateTicketAsync(_tenantId, _userId, Ticket());
        var detail = await _service.GetTicketByIdAsync(_tenantId, ticket.Id);

        detail.Subject.Should().Be("Login sorunu");
        detail.Body.Should().Be("Giriş yapamıyorum");
        detail.Messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTicketById_NonExistent_ShouldThrow()
    {
        var act = () => _service.GetTicketByIdAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Destek talebi bulunamadı*");
    }

    [Fact]
    public async Task GetTicketById_WrongTenant_ShouldThrow()
    {
        var ticket = await _service.CreateTicketAsync(_tenantId, _userId, Ticket());
        var act = () => _service.GetTicketByIdAsync(Guid.NewGuid(), ticket.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // REPLY
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reply_ShouldAddMessage()
    {
        var ticket = await _service.CreateTicketAsync(_tenantId, _userId, Ticket());
        var reply = await _service.ReplyAsync(_tenantId, ticket.Id, _adminId,
            new ReplyTicketRequest("Şifrenizi sıfırlayın."));

        reply.Body.Should().Be("Şifrenizi sıfırlayın.");
        reply.SenderName.Should().Contain("Hoca");
    }

    [Fact]
    public async Task Reply_ShouldSetStatusToInProgress()
    {
        var ticket = await _service.CreateTicketAsync(_tenantId, _userId, Ticket());
        await _service.ReplyAsync(_tenantId, ticket.Id, _adminId, new ReplyTicketRequest("Bakıyoruz."));

        var dbTicket = await _db.SupportTickets.FindAsync(ticket.Id);
        dbTicket!.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public async Task Reply_NonExistentTicket_ShouldThrow()
    {
        var act = () => _service.ReplyAsync(_tenantId, Guid.NewGuid(), _adminId,
            new ReplyTicketRequest("Test"));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Reply_MultipleMessages_ShouldAllBeVisible()
    {
        var ticket = await _service.CreateTicketAsync(_tenantId, _userId, Ticket());
        await _service.ReplyAsync(_tenantId, ticket.Id, _adminId, new ReplyTicketRequest("Mesaj 1"));
        await _service.ReplyAsync(_tenantId, ticket.Id, _userId, new ReplyTicketRequest("Mesaj 2"));
        await _service.ReplyAsync(_tenantId, ticket.Id, _adminId, new ReplyTicketRequest("Mesaj 3"));

        var detail = await _service.GetTicketByIdAsync(_tenantId, ticket.Id);
        detail.Messages.Should().HaveCount(4);
    }

    // ────────────────────────────────────────────────────────────────────────
    // CLOSE
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseTicket_ShouldSetStatusToClosed()
    {
        var ticket = await _service.CreateTicketAsync(_tenantId, _userId, Ticket());
        await _service.CloseTicketAsync(_tenantId, ticket.Id);

        var dbTicket = await _db.SupportTickets.FindAsync(ticket.Id);
        dbTicket!.Status.Should().Be(TicketStatus.Closed);
    }

    [Fact]
    public async Task CloseTicket_NonExistent_ShouldThrow()
    {
        var act = () => _service.CloseTicketAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // TICKET LIST (Paging, Status Filter)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTickets_Paging_ShouldWork()
    {
        for (int i = 0; i < 15; i++)
            await _service.CreateTicketAsync(_tenantId, _userId, Ticket($"Talep {i}"));

        var p1 = await _service.GetTicketsAsync(_tenantId, 1, 10, null);
        var p2 = await _service.GetTicketsAsync(_tenantId, 2, 10, null);

        p1.Items.Should().HaveCount(10);
        p2.Items.Should().HaveCount(5);
        p1.TotalCount.Should().Be(15);
    }

    [Fact]
    public async Task GetTickets_FilterByStatus_ShouldFilter()
    {
        var t1 = await _service.CreateTicketAsync(_tenantId, _userId, Ticket("Açık Talep"));
        var t2 = await _service.CreateTicketAsync(_tenantId, _userId, Ticket("Kapalı Talep"));
        await _service.CloseTicketAsync(_tenantId, t2.Id);

        var closed = await _service.GetTicketsAsync(_tenantId, 1, 10, "Closed");
        closed.Items.Should().HaveCount(1);
        closed.Items[0].Subject.Should().Be("Kapalı Talep");
    }

    // ────────────────────────────────────────────────────────────────────────
    // FAQ
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateFaq_ShouldSave()
    {
        var faq = await _service.CreateFaqAsync(_tenantId,
            new CreateFaqRequest("Nasıl kayıt olurum?", "Ana sayfadan...", "Kayıt", null));

        faq.QuestionText.Should().Be("Nasıl kayıt olurum?");
        faq.AnswerText.Should().Be("Ana sayfadan...");
    }

    [Fact]
    public async Task CreateFaq_AutoSortOrder_ShouldIncrement()
    {
        var f1 = await _service.CreateFaqAsync(_tenantId, new CreateFaqRequest("S1", "A1", null, null));
        var f2 = await _service.CreateFaqAsync(_tenantId, new CreateFaqRequest("S2", "A2", null, null));
        var f3 = await _service.CreateFaqAsync(_tenantId, new CreateFaqRequest("S3", "A3", null, null));

        f1.SortOrder.Should().Be(1);
        f2.SortOrder.Should().Be(2);
        f3.SortOrder.Should().Be(3);
    }

    [Fact]
    public async Task GetFaqs_ShouldReturnAllOrdered()
    {
        await _service.CreateFaqAsync(_tenantId, new CreateFaqRequest("Z", "Z", null, 3));
        await _service.CreateFaqAsync(_tenantId, new CreateFaqRequest("A", "A", null, 1));
        await _service.CreateFaqAsync(_tenantId, new CreateFaqRequest("M", "M", null, 2));

        var faqs = await _service.GetFaqsAsync(_tenantId);
        faqs.Should().HaveCount(3);
        faqs[0].QuestionText.Should().Be("A"); // SortOrder 1
        faqs[2].QuestionText.Should().Be("Z"); // SortOrder 3
    }

    [Fact]
    public async Task UpdateFaq_ShouldModifyFields()
    {
        var faq = await _service.CreateFaqAsync(_tenantId, new CreateFaqRequest("Eski", "Yanıt", null, null));
        var updated = await _service.UpdateFaqAsync(_tenantId, faq.Id,
            new UpdateFaqRequest("Yeni Soru", "Yeni Yanıt", "Genel", null));

        updated.QuestionText.Should().Be("Yeni Soru");
        updated.AnswerText.Should().Be("Yeni Yanıt");
        updated.Category.Should().Be("Genel");
    }

    [Fact]
    public async Task UpdateFaq_NonExistent_ShouldThrow()
    {
        var act = () => _service.UpdateFaqAsync(_tenantId, Guid.NewGuid(),
            new UpdateFaqRequest("X", null, null, null));
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*SSS bulunamadı*");
    }

    [Fact]
    public async Task DeleteFaq_ShouldRemove()
    {
        var faq = await _service.CreateFaqAsync(_tenantId, new CreateFaqRequest("Sil", "Beni", null, null));
        await _service.DeleteFaqAsync(_tenantId, faq.Id);
        (await _db.Faqs.AnyAsync(f => f.Id == faq.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFaq_NonExistent_ShouldThrow()
    {
        var act = () => _service.DeleteFaqAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    public void Dispose() => _db.Dispose();
}
