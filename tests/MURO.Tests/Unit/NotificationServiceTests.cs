using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Notifications;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// NotificationService testleri — bildirim oluşturma, toplu gönderim,
/// okundu işaretleme, SignalR push, e-posta/SMS gönderimi.
/// </summary>
public class NotificationServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly NotificationService _service;
    private readonly Mock<INotificationPush> _pushMock;
    private readonly Mock<IEmailSender> _emailMock;
    private readonly Mock<ISmsSender> _smsMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _user1 = Guid.NewGuid();
    private readonly Guid _user2 = Guid.NewGuid();
    private readonly Guid _user3 = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _pushMock = new Mock<INotificationPush>();
        _emailMock = new Mock<IEmailSender>();
        _smsMock = new Mock<ISmsSender>();

        _service = new NotificationService(_db, _pushMock.Object, _emailMock.Object, _smsMock.Object, new MURO.Tests.Helpers.DummyCacheService());

        // Seed
        var tenant = new Tenant { Id = _tenantId, Name = "Test", Code = "test", CreatedAt = DateTime.UtcNow };
        _db.Tenants.Add(tenant);
        _db.Users.Add(new User { Id = _user1, Email = "u1@t.com", Phone = "5551234567", FirstName = "Ali", LastName = "Yılmaz", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Users.Add(new User { Id = _user2, Email = "u2@t.com", FirstName = "Ayşe", LastName = "Demir", PasswordHash = "h", Role = UserRole.Student, IsActive = true }); // Telefon yok
        _db.Users.Add(new User { Id = _user3, Email = "u3@t.com", Phone = "5559876543", FirstName = "Veli", LastName = "Kaya", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.TenantMemberships.Add(new TenantMembership { Id = Guid.NewGuid(), UserId = _user1, TenantId = _tenantId, Role = UserRole.Student, Status = "active" });
        _db.TenantMemberships.Add(new TenantMembership { Id = Guid.NewGuid(), UserId = _user2, TenantId = _tenantId, Role = UserRole.Student, Status = "active" });
        _db.TenantMemberships.Add(new TenantMembership { Id = Guid.NewGuid(), UserId = _user3, TenantId = _tenantId, Role = UserRole.Student, Status = "active" });
        _db.Groups.Add(new Group { Id = _groupId, TenantId = _tenantId, Name = "Sınıf A" });
        _db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = _groupId, UserId = _user1, Status = "active" });
        _db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = _groupId, UserId = _user2, Status = "active" });
        _db.SaveChanges();
    }

    // ── 1. Tekil bildirim oluşturma ─────────────────────────────────────────
    [Fact]
    public async Task Create_ShouldSaveAndPush()
    {
        var result = await _service.CreateAsync(_tenantId, new CreateNotificationRequest(
            _user1, "Test Başlık", "Test body", "Info", "InApp"));

        result.Title.Should().Be("Test Başlık");

        var dbNotif = await _db.Notifications.FirstAsync(n => n.UserId == _user1);
        dbNotif.Should().NotBeNull();
        dbNotif.IsRead.Should().BeFalse();

        _pushMock.Verify(p => p.PushToUserAsync(_user1.ToString(), It.IsAny<NotificationDto>()), Times.Once);
    }

    // ── 2. Tekil bildirim → e-posta gidiyor mu ──────────────────────────────
    [Fact]
    public async Task Create_ShouldSendEmail()
    {
        await _service.CreateAsync(_tenantId, new CreateNotificationRequest(
            _user1, "Ödev", "Ödeviniz var", "Homework", "InApp"));

        _emailMock.Verify(e => e.SendAsync("u1@t.com", "Ali Yılmaz", "Ödev", It.Is<string>(b => b.Contains("Ödeviniz var"))), Times.Once);
    }

    // ── 3. Tekil bildirim → Telefonu olan → SMS gidiyor ─────────────────────
    [Fact]
    public async Task Create_WithPhone_ShouldSendSms()
    {
        await _service.CreateAsync(_tenantId, new CreateNotificationRequest(
            _user1, "SMS Test", "Mesaj", "Info", "InApp"));

        _smsMock.Verify(s => s.SendAsync("5551234567", It.Is<string>(m => m.Contains("SMS Test"))), Times.Once);
    }

    // ── 4. Tekil bildirim → Telefonsuz → SMS gitmemeli ──────────────────────
    [Fact]
    public async Task Create_WithoutPhone_ShouldNotSendSms()
    {
        await _service.CreateAsync(_tenantId, new CreateNotificationRequest(
            _user2, "No SMS", "Mesaj", "Info", "InApp"));

        _smsMock.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── 5. Toplu gönderim → UserIds ─────────────────────────────────────────
    [Fact]
    public async Task BulkSend_ToUserIds_ShouldSendToAll()
    {
        var count = await _service.BulkSendAsync(_tenantId, new BulkNotificationRequest(
            new List<Guid> { _user1, _user2, _user3 },
            "Toplu", "Herkese mesaj", "Announcement"));

        count.Should().Be(3);
        var dbCount = await _db.Notifications.CountAsync(n => n.Title == "Toplu");
        dbCount.Should().Be(3);

        _pushMock.Verify(p => p.PushToUserAsync(It.IsAny<string>(), It.IsAny<NotificationDto>()), Times.Exactly(3));
    }

    // ── 6. Toplu gönderim → Gruba ───────────────────────────────────────────
    [Fact]
    public async Task BulkSend_ToGroup_ShouldSendToGroupMembers()
    {
        var count = await _service.BulkSendAsync(_tenantId, new BulkNotificationRequest(
            new List<Guid>(), "Grup Bildirim", "Sınıf A'ya mesaj", "Info", _groupId));

        count.Should().Be(2); // Grupta 2 kişi var
    }

    // ── 7. Toplu gönderim → Tüm tenant ─────────────────────────────────────
    [Fact]
    public async Task BulkSend_ToAll_ShouldSendToAllActiveMembers()
    {
        var count = await _service.BulkSendAsync(_tenantId, new BulkNotificationRequest(
            new List<Guid>(), "Genel", "Herkese", "System", null, true));

        count.Should().Be(3); // 3 aktif üye
    }

    // ── 8. Toplu gönderim → Boş liste → 0 ──────────────────────────────────
    [Fact]
    public async Task BulkSend_EmptyList_ShouldReturnZero()
    {
        var count = await _service.BulkSendAsync(_tenantId, new BulkNotificationRequest(
            new List<Guid>(), "Boş", "Kimseye", "Info"));

        count.Should().Be(0);
    }

    // ── 9. Okundu işaretle ──────────────────────────────────────────────────
    [Fact]
    public async Task MarkAsRead_ShouldUpdateIsRead()
    {
        var dto = await _service.CreateAsync(_tenantId, new CreateNotificationRequest(
            _user1, "Okunacak", "Body", "Info", "InApp"));

        await _service.MarkAsReadAsync(dto.Id);

        var n = await _db.Notifications.FindAsync(dto.Id);
        n!.IsRead.Should().BeTrue();
    }

    // ── 10. Tümünü okundu işaretle ──────────────────────────────────────────
    // NOT: ExecuteUpdateAsync InMemory DB'de desteklenmiyor → PostgreSQL integration test'te çalışır
    [Fact(Skip = "ExecuteUpdateAsync InMemory provider'da desteklenmiyor")]
    public async Task MarkAllRead_ShouldMarkAllForUser()
    {
        await _service.CreateAsync(_tenantId, new CreateNotificationRequest(_user1, "N1", "B", "I", "InApp"));
        await _service.CreateAsync(_tenantId, new CreateNotificationRequest(_user1, "N2", "B", "I", "InApp"));
        await _service.CreateAsync(_tenantId, new CreateNotificationRequest(_user2, "N3", "B", "I", "InApp"));

        await _service.MarkAllReadAsync(_user1);

        var unread1 = await _service.GetUnreadCountAsync(_user1);
        var unread2 = await _service.GetUnreadCountAsync(_user2);
        unread1.Should().Be(0, "User1'in tümü okundu");
        unread2.Should().Be(1, "User2'ninki değişmedi");
    }

    // ── 11. Okunmamış sayısı ────────────────────────────────────────────────
    [Fact]
    public async Task GetUnreadCount_ShouldReturnCorrect()
    {
        await _service.CreateAsync(_tenantId, new CreateNotificationRequest(_user1, "A", "B", "I", "InApp"));
        await _service.CreateAsync(_tenantId, new CreateNotificationRequest(_user1, "B", "B", "I", "InApp"));

        var count = await _service.GetUnreadCountAsync(_user1);
        count.Should().Be(2);
    }

    // ── 12. Sayfalama ───────────────────────────────────────────────────────
    [Fact]
    public async Task GetNotifications_Paging_ShouldWork()
    {
        for (int i = 0; i < 15; i++)
            await _service.CreateAsync(_tenantId, new CreateNotificationRequest(_user1, $"N{i}", "B", "I", "InApp"));

        var page1 = await _service.GetUserNotificationsAsync(_tenantId, _user1, 1, 10, null);
        var page2 = await _service.GetUserNotificationsAsync(_tenantId, _user1, 2, 10, null);

        page1.Items.Should().HaveCount(10);
        page2.Items.Should().HaveCount(5);
        page1.TotalCount.Should().Be(15);
    }

    // ── 13. Sadece okunmamışlar filtresi ────────────────────────────────────
    [Fact]
    public async Task GetNotifications_UnreadOnly_ShouldFilter()
    {
        var n1 = await _service.CreateAsync(_tenantId, new CreateNotificationRequest(_user1, "Read", "B", "I", "InApp"));
        await _service.CreateAsync(_tenantId, new CreateNotificationRequest(_user1, "Unread", "B", "I", "InApp"));
        await _service.MarkAsReadAsync(n1.Id);

        var result = await _service.GetUserNotificationsAsync(_tenantId, _user1, 1, 10, true);
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Unread");
    }

    public void Dispose() => _db.Dispose();
}
