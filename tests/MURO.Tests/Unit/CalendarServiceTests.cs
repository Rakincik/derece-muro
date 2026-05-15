using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Calendar;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// CalendarService için unit testler — Etkinlik CRUD,
/// tarih filtresi, grup filtresi, tenant izolasyonu.
/// </summary>
public class CalendarServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly CalendarService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public CalendarServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _service = new CalendarService(_db, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "cal", CreatedAt = DateTime.UtcNow });
        _db.Groups.Add(new Group { Id = _groupId, TenantId = _tenantId, Name = "Fizik A" });
        _db.SaveChanges();
    }

    private CreateCalendarEventRequest Evt(string title = "Matematik Dersi",
        DateTime? start = null, DateTime? end = null, Guid? groupId = null) =>
        new(title, "Canlı ders", "Ders",
            start ?? DateTime.UtcNow.AddDays(1),
            end ?? DateTime.UtcNow.AddDays(1).AddHours(2),
            groupId, null, "#4ade80");

    // ────────────────────────────────────────────────────────────────────────
    // CREATE
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEvent_ShouldSaveToDb()
    {
        var result = await _service.CreateEventAsync(_tenantId, Evt());

        result.Title.Should().Be("Matematik Dersi");
        result.EventType.Should().Be("Ders");
        result.Color.Should().Be("#4ade80");
        (await _db.CalendarEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateEvent_WithGroup_ShouldSetGroupId()
    {
        var result = await _service.CreateEventAsync(_tenantId, Evt(groupId: _groupId));
        result.GroupId.Should().Be(_groupId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET BY ID
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEventById_ShouldReturn()
    {
        var created = await _service.CreateEventAsync(_tenantId, Evt());
        var detail = await _service.GetEventByIdAsync(_tenantId, created.Id);

        detail.Title.Should().Be("Matematik Dersi");
        detail.Description.Should().Be("Canlı ders");
    }

    [Fact]
    public async Task GetEventById_NonExistent_ShouldThrow()
    {
        var act = () => _service.GetEventByIdAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Etkinlik bulunamadı*");
    }

    [Fact]
    public async Task GetEventById_WrongTenant_ShouldThrow()
    {
        var created = await _service.CreateEventAsync(_tenantId, Evt());
        var act = () => _service.GetEventByIdAsync(Guid.NewGuid(), created.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // UPDATE
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEvent_ShouldModifyFields()
    {
        var created = await _service.CreateEventAsync(_tenantId, Evt());
        var updated = await _service.UpdateEventAsync(_tenantId, created.Id,
            new UpdateCalendarEventRequest("Fizik Dersi", null, "Sınav", null, null, null, null, "#ff0000"));

        updated.Title.Should().Be("Fizik Dersi");
        updated.EventType.Should().Be("Sınav");
        updated.Color.Should().Be("#ff0000");
    }

    [Fact]
    public async Task UpdateEvent_PartialUpdate_ShouldKeepOtherFields()
    {
        var created = await _service.CreateEventAsync(_tenantId, Evt());
        var updated = await _service.UpdateEventAsync(_tenantId, created.Id,
            new UpdateCalendarEventRequest("Yeni Başlık", null, null, null, null, null, null, null));

        updated.Title.Should().Be("Yeni Başlık");
        updated.EventType.Should().Be("Ders"); // Değişmemiş olmalı
    }

    [Fact]
    public async Task UpdateEvent_NonExistent_ShouldThrow()
    {
        var act = () => _service.UpdateEventAsync(_tenantId, Guid.NewGuid(),
            new UpdateCalendarEventRequest("X", null, null, null, null, null, null, null));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // DELETE
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEvent_ShouldRemove()
    {
        var created = await _service.CreateEventAsync(_tenantId, Evt());
        await _service.DeleteEventAsync(_tenantId, created.Id);
        (await _db.CalendarEvents.AnyAsync(e => e.Id == created.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteEvent_NonExistent_ShouldThrow()
    {
        var act = () => _service.DeleteEventAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteEvent_WrongTenant_ShouldThrow()
    {
        var created = await _service.CreateEventAsync(_tenantId, Evt());
        var act = () => _service.DeleteEventAsync(Guid.NewGuid(), created.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET EVENTS (Tarih & Grup Filtresi)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvents_DateFilter_ShouldReturnInRange()
    {
        await _service.CreateEventAsync(_tenantId, Evt("Bu hafta",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)));
        await _service.CreateEventAsync(_tenantId, Evt("Gelecek ay",
            DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(31)));

        var events = await _service.GetEventsAsync(_tenantId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7), null);

        events.Should().HaveCount(1);
        events[0].Title.Should().Be("Bu hafta");
    }

    [Fact]
    public async Task GetEvents_GroupFilter_ShouldReturnOnlyGroupEvents()
    {
        await _service.CreateEventAsync(_tenantId, Evt("Fizik", groupId: _groupId));
        await _service.CreateEventAsync(_tenantId, Evt("Genel"));

        var events = await _service.GetEventsAsync(_tenantId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7), _groupId);

        events.Should().HaveCount(1);
        events[0].Title.Should().Be("Fizik");
    }

    [Fact]
    public async Task GetEvents_DifferentTenant_ShouldReturnEmpty()
    {
        await _service.CreateEventAsync(_tenantId, Evt());

        var events = await _service.GetEventsAsync(Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7), null);

        events.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEvents_ShouldOrderByStartDate()
    {
        await _service.CreateEventAsync(_tenantId, Evt("İkinci",
            DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)));
        await _service.CreateEventAsync(_tenantId, Evt("Birinci",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)));

        var events = await _service.GetEventsAsync(_tenantId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7), null);

        events[0].Title.Should().Be("Birinci");
        events[1].Title.Should().Be("İkinci");
    }

    public void Dispose() => _db.Dispose();
}
