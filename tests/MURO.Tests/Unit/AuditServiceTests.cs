using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// AuditService için unit testler — Audit log kaydetme, sayfalama,
/// action/entityType/userId/tarih aralığı filtreleri, tenant izolasyonu.
/// </summary>
public class AuditServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly AuditService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _service = new AuditService(_db, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "audit" });
        _db.SaveChanges();
    }

    // ════════════════════════════════════════════════════════════════════════
    // LOG — Kayıt oluşturma
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Log_ShouldSaveToDb()
    {
        await _service.LogAsync(_tenantId, _userId, "Ali", "Login",
            "User", _userId.ToString(), "Ali Y", "Giriş yaptı", "192.168.1.1");

        var count = await _db.AuditLogs.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Log_ShouldPreserveAllFields()
    {
        await _service.LogAsync(_tenantId, _userId, "Hasan", "Create",
            "Course", "c1", "Fizik 101", "Yeni kurs", "10.0.0.1");

        var log = await _db.AuditLogs.FirstAsync();
        log.TenantId.Should().Be(_tenantId);
        log.UserId.Should().Be(_userId);
        log.UserName.Should().Be("Hasan");
        log.Action.Should().Be("Create");
        log.EntityType.Should().Be("Course");
        log.EntityId.Should().Be("c1");
        log.EntityName.Should().Be("Fizik 101");
        log.Details.Should().Be("Yeni kurs");
        log.IpAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public async Task Log_NullableFields_ShouldAcceptNulls()
    {
        await _service.LogAsync(null, null, null, "SystemStart",
            "System", null, null);

        (await _db.AuditLogs.CountAsync()).Should().Be(1);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GET LOGS — Sayfalama
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetLogs_Paging_ShouldWork()
    {
        for (int i = 0; i < 25; i++)
            await _service.LogAsync(_tenantId, _userId, "U", "Action", "Entity", null, null);

        var p1 = await _service.GetLogsAsync(_tenantId, 1, 10);
        var p2 = await _service.GetLogsAsync(_tenantId, 2, 10);
        var p3 = await _service.GetLogsAsync(_tenantId, 3, 10);

        p1.Items.Should().HaveCount(10);
        p2.Items.Should().HaveCount(10);
        p3.Items.Should().HaveCount(5);
        p1.TotalCount.Should().Be(25);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GET LOGS — Filtreler
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetLogs_FilterByAction_ShouldFilter()
    {
        await _service.LogAsync(_tenantId, _userId, "U", "Login", "User", null, null);
        await _service.LogAsync(_tenantId, _userId, "U", "Create", "Course", null, null);
        await _service.LogAsync(_tenantId, _userId, "U", "Login", "User", null, null);

        var result = await _service.GetLogsAsync(_tenantId, 1, 10, action: "Login");
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetLogs_FilterByEntityType_ShouldFilter()
    {
        await _service.LogAsync(_tenantId, _userId, "U", "Create", "Course", null, null);
        await _service.LogAsync(_tenantId, _userId, "U", "Create", "User", null, null);

        var result = await _service.GetLogsAsync(_tenantId, 1, 10, entityType: "Course");
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetLogs_FilterByUserId_ShouldFilter()
    {
        var otherUser = Guid.NewGuid();
        await _service.LogAsync(_tenantId, _userId, "Ali", "Login", "User", null, null);
        await _service.LogAsync(_tenantId, otherUser, "Veli", "Login", "User", null, null);

        var result = await _service.GetLogsAsync(_tenantId, 1, 10, userId: _userId.ToString());
        result.Items.Should().HaveCount(1);
        result.Items[0].UserName.Should().Be("Ali");
    }

    [Fact]
    public async Task GetLogs_FilterByInvalidUserId_ShouldReturnAll()
    {
        await _service.LogAsync(_tenantId, _userId, "U", "Login", "User", null, null);
        // Geçersiz GUID → filtre uygulanmaz, hepsi döner
        var result = await _service.GetLogsAsync(_tenantId, 1, 10, userId: "not-a-guid");
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetLogs_FilterByDateRange_ShouldFilter()
    {
        // Eski log
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = _tenantId, UserId = _userId, UserName = "U",
            Action = "Old", EntityType = "X",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        });
        // Yeni log
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = _tenantId, UserId = _userId, UserName = "U",
            Action = "New", EntityType = "X",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetLogsAsync(_tenantId, 1, 10,
            from: DateTime.UtcNow.AddDays(-7), to: DateTime.UtcNow.AddDays(1));
        result.Items.Should().HaveCount(1);
        result.Items[0].Action.Should().Be("New");
    }

    // ════════════════════════════════════════════════════════════════════════
    // TENANT İZOLASYONU
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetLogs_TenantIsolation_ShouldNotShowOtherTenantLogs()
    {
        var otherTenant = Guid.NewGuid();
        await _service.LogAsync(_tenantId, _userId, "U", "Login", "User", null, null);
        await _service.LogAsync(otherTenant, _userId, "U", "Login", "User", null, null);

        var result = await _service.GetLogsAsync(_tenantId, 1, 10);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetLogs_ShouldOrderByCreatedAtDescending()
    {
        await _service.LogAsync(_tenantId, _userId, "U", "First", "X", null, null);
        await Task.Delay(10);
        await _service.LogAsync(_tenantId, _userId, "U", "Second", "X", null, null);

        var result = await _service.GetLogsAsync(_tenantId, 1, 10);
        result.Items[0].Action.Should().Be("Second"); // En yeni ilk sırada
    }

    // ════════════════════════════════════════════════════════════════════════
    // COMBINED FILTERS
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetLogs_CombinedFilters_ShouldIntersect()
    {
        await _service.LogAsync(_tenantId, _userId, "Ali", "Create", "Course", null, null);
        await _service.LogAsync(_tenantId, _userId, "Ali", "Delete", "Course", null, null);
        await _service.LogAsync(_tenantId, _userId, "Ali", "Create", "User", null, null);

        var result = await _service.GetLogsAsync(_tenantId, 1, 10, action: "Create", entityType: "Course");
        result.Items.Should().HaveCount(1);
    }

    public void Dispose() => _db.Dispose();
}
