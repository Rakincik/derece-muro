using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Videos;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// VideoNoteService için unit testler — Not ekleme, güncelleme, silme,
/// timestamp formatlama, kullanıcı izolasyonu.
/// </summary>
public class VideoNoteServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly VideoNoteService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Guid _mediaAssetId = Guid.NewGuid();

    public VideoNoteServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _service = new VideoNoteService(_db, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "vn", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User { Id = _userId, Email = "u1@t.com", FirstName = "Ali", LastName = "U", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Users.Add(new User { Id = _otherUserId, Email = "u2@t.com", FirstName = "Veli", LastName = "U", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.MediaAssets.Add(new MediaAsset { Id = _mediaAssetId, Title = "Ders 1", TenantId = _tenantId, Status = MediaStatus.Ready });
        _db.SaveChanges();
    }

    // ────────────────────────────────────────────────────────────────────────
    // ADD NOTE
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddNote_ShouldSaveToDb()
    {
        var note = await _service.AddNoteAsync(_userId, _mediaAssetId,
            new CreateVideoNoteRequest(120, "Önemli formül"));

        note.Text.Should().Be("Önemli formül");
        note.TimestampSeconds.Should().Be(120);
        note.MediaAssetId.Should().Be(_mediaAssetId);
        (await _db.VideoNotes.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(90, "01:30")]        // 1 dakika 30 saniye
    [InlineData(3600, "01:00:00")]   // 1 saat
    [InlineData(3661, "01:01:01")]   // 1 saat 1 dakika 1 saniye
    [InlineData(0, "00:00")]         // Başlangıç
    [InlineData(59, "00:59")]        // 59 saniye
    public async Task AddNote_TimestampFormat_ShouldBeCorrect(int seconds, string expected)
    {
        var note = await _service.AddNoteAsync(_userId, _mediaAssetId,
            new CreateVideoNoteRequest(seconds, "Not"));

        note.TimestampFormatted.Should().Be(expected);
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET MY NOTES
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyNotes_ShouldReturnOnlyMyNotes()
    {
        await _service.AddNoteAsync(_userId, _mediaAssetId, new CreateVideoNoteRequest(30, "Benim not"));
        await _service.AddNoteAsync(_otherUserId, _mediaAssetId, new CreateVideoNoteRequest(60, "Başkasının notu"));

        var myNotes = await _service.GetMyNotesAsync(_userId, _mediaAssetId);

        myNotes.Should().HaveCount(1);
        myNotes[0].Text.Should().Be("Benim not");
    }

    [Fact]
    public async Task GetMyNotes_ShouldOrderByTimestamp()
    {
        await _service.AddNoteAsync(_userId, _mediaAssetId, new CreateVideoNoteRequest(300, "Geç"));
        await _service.AddNoteAsync(_userId, _mediaAssetId, new CreateVideoNoteRequest(10, "Erken"));
        await _service.AddNoteAsync(_userId, _mediaAssetId, new CreateVideoNoteRequest(120, "Orta"));

        var notes = await _service.GetMyNotesAsync(_userId, _mediaAssetId);

        notes[0].TimestampSeconds.Should().Be(10);
        notes[1].TimestampSeconds.Should().Be(120);
        notes[2].TimestampSeconds.Should().Be(300);
    }

    [Fact]
    public async Task GetMyNotes_DifferentMediaAsset_ShouldReturnSeparate()
    {
        var otherAssetId = Guid.NewGuid();
        _db.MediaAssets.Add(new MediaAsset { Id = otherAssetId, Title = "Ders 2", TenantId = _tenantId, Status = MediaStatus.Ready });
        await _db.SaveChangesAsync();

        await _service.AddNoteAsync(_userId, _mediaAssetId, new CreateVideoNoteRequest(30, "Video 1 notu"));
        await _service.AddNoteAsync(_userId, otherAssetId, new CreateVideoNoteRequest(60, "Video 2 notu"));

        var notes1 = await _service.GetMyNotesAsync(_userId, _mediaAssetId);
        var notes2 = await _service.GetMyNotesAsync(_userId, otherAssetId);

        notes1.Should().HaveCount(1);
        notes2.Should().HaveCount(1);
    }

    // ────────────────────────────────────────────────────────────────────────
    // UPDATE NOTE
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateNote_ShouldChangeText()
    {
        var note = await _service.AddNoteAsync(_userId, _mediaAssetId,
            new CreateVideoNoteRequest(120, "Eski metin"));

        var updated = await _service.UpdateNoteAsync(_userId, note.Id,
            new UpdateVideoNoteRequest("Yeni metin"));

        updated.Text.Should().Be("Yeni metin");
        updated.TimestampSeconds.Should().Be(120); // Timestamp değişmemeli
    }

    [Fact]
    public async Task UpdateNote_OtherUser_ShouldThrow()
    {
        var note = await _service.AddNoteAsync(_userId, _mediaAssetId,
            new CreateVideoNoteRequest(120, "Benim notum"));

        var act = () => _service.UpdateNoteAsync(_otherUserId, note.Id,
            new UpdateVideoNoteRequest("Hack denemesi"));

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Not bulunamadı*");
    }

    [Fact]
    public async Task UpdateNote_NonExistent_ShouldThrow()
    {
        var act = () => _service.UpdateNoteAsync(_userId, Guid.NewGuid(),
            new UpdateVideoNoteRequest("Yok"));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // DELETE NOTE
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteNote_ShouldRemove()
    {
        var note = await _service.AddNoteAsync(_userId, _mediaAssetId,
            new CreateVideoNoteRequest(30, "Silinecek"));

        await _service.DeleteNoteAsync(_userId, note.Id);
        (await _db.VideoNotes.AnyAsync(n => n.Id == note.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteNote_OtherUser_ShouldThrow()
    {
        var note = await _service.AddNoteAsync(_userId, _mediaAssetId,
            new CreateVideoNoteRequest(30, "Benim notum"));

        var act = () => _service.DeleteNoteAsync(_otherUserId, note.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteNote_NonExistent_ShouldThrow()
    {
        var act = () => _service.DeleteNoteAsync(_userId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    public void Dispose() => _db.Dispose();
}
