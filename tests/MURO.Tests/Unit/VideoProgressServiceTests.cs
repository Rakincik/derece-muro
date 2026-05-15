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
/// VideoProgressService için unit testler — Heartbeat ilerleme kaydı,
/// %90 tamamlanma, FK koruması (MediaAsset kontrolü), kurs ilerleme raporu.
/// </summary>
public class VideoProgressServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly VideoProgressService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _mediaAssetId = Guid.NewGuid();

    public VideoProgressServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        ICacheService cacheMock = new MURO.Tests.Helpers.DummyCacheService();
        _service = new VideoProgressService(_db, cacheMock);

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "vp", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User { Id = _userId, Email = "u@t.com", FirstName = "Ali", LastName = "Y", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Fizik 101" });
        _db.MediaAssets.Add(new MediaAsset { Id = _mediaAssetId, Title = "Video 1", TenantId = _tenantId, CourseId = _courseId, Status = MediaStatus.Ready });
        _db.SaveChanges();
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET PROGRESS
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProgress_NoRecord_ShouldReturnNull()
    {
        var result = await _service.GetProgressAsync(_userId, _mediaAssetId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProgress_AfterUpsert_ShouldReturnProgress()
    {
        await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(120, 600, 120));

        var result = await _service.GetProgressAsync(_userId, _mediaAssetId);

        result.Should().NotBeNull();
        result!.WatchedSeconds.Should().Be(120);
        result.TotalSeconds.Should().Be(600);
    }

    // ────────────────────────────────────────────────────────────────────────
    // UPSERT PROGRESS
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertProgress_NewRecord_ShouldCreate()
    {
        var result = await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(60, 600, 60));

        result.Should().NotBeNull();
        result!.WatchedSeconds.Should().Be(60);
        result.CompletionPercentage.Should().Be(10); // 60/600 = 10%
        (await _db.VideoProgresses.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpsertProgress_ExistingRecord_ShouldUpdate()
    {
        // İlk heartbeat
        await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(60, 600, 60));

        // İkinci heartbeat
        var result = await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(120, 600, 120));

        result!.WatchedSeconds.Should().Be(120);
        result.LastPosition.Should().Be(120);
        (await _db.VideoProgresses.CountAsync()).Should().Be(1); // Yeni kayıt oluşmamalı
    }

    [Fact]
    public async Task UpsertProgress_90Percent_ShouldMarkCompleted()
    {
        // %90+ izlenirse tamamlanmış sayılmalı
        var result = await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(540, 600, 540)); // 540/600 = %90

        result!.CompletedAt.Should().NotBeNull();
        result.CompletionPercentage.Should().Be(90);
    }

    [Fact]
    public async Task UpsertProgress_Below90Percent_ShouldNotMarkCompleted()
    {
        var result = await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(500, 600, 500)); // 500/600 = %83.3

        result!.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpsertProgress_MarkCompleted_ShouldForceComplete()
    {
        var result = await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(60, 600, 60, MarkCompleted: true));

        result!.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpsertProgress_SkipAndReplayCounts_ShouldAccumulate()
    {
        // İlk heartbeat: 2 skip, 1 replay
        await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(60, 600, 60, 2, 1));

        // İkinci heartbeat: 1 skip, 3 replay
        await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(120, 600, 120, 1, 3));

        var progress = await _db.VideoProgresses.FirstAsync(vp => vp.UserId == _userId);
        progress.SkipCount.Should().Be(3);  // 2 + 1
        progress.ReplayCount.Should().Be(4); // 1 + 3
    }

    // ────────────────────────────────────────────────────────────────────────
    // FK KORUMASI — MediaAsset var mı kontrolü
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertProgress_InvalidMediaAssetId_ShouldReturnNull()
    {
        // Recording ID gibi geçersiz bir ID ile çağır
        var fakeId = Guid.NewGuid();
        var result = await _service.UpsertProgressAsync(_userId, fakeId,
            new UpdateVideoProgressRequest(60, 600, 60));

        result.Should().BeNull(); // FK violation yerine null dönmeli
        (await _db.VideoProgresses.CountAsync()).Should().Be(0); // DB'ye yazılmamalı
    }

    // ────────────────────────────────────────────────────────────────────────
    // CompletionPercentage Hesaplama
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 600, 0)]      // Hiç izlenmemiş
    [InlineData(300, 600, 50)]   // Yarısı
    [InlineData(600, 600, 100)]  // Tamamı
    [InlineData(90, 100, 90)]    // Tam %90
    public async Task CompletionPercentage_ShouldBeCorrect(int watched, int total, double expected)
    {
        var result = await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(watched, total, watched));

        result!.CompletionPercentage.Should().Be(expected);
    }

    [Fact]
    public async Task CompletionPercentage_ZeroTotal_ShouldBeZero()
    {
        var result = await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(0, 0, 0));

        result!.CompletionPercentage.Should().Be(0);
    }

    // ────────────────────────────────────────────────────────────────────────
    // CompletedAt — İkinci kez tamamlanma zamanını değiştirmemeli
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertProgress_AlreadyCompleted_ShouldNotResetCompletedAt()
    {
        // İlk tamamlanma
        await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(540, 600, 540)); // %90 → tamamlandı

        var firstCompletedAt = (await _db.VideoProgresses.FirstAsync()).CompletedAt;

        // Devam izleme
        await Task.Delay(10); // Slight delay to differentiate timestamps
        await _service.UpsertProgressAsync(_userId, _mediaAssetId,
            new UpdateVideoProgressRequest(600, 600, 600));

        var secondCompletedAt = (await _db.VideoProgresses.FirstAsync()).CompletedAt;
        secondCompletedAt.Should().Be(firstCompletedAt); // CompletedAt değişmemeli
    }

    public void Dispose() => _db.Dispose();
}
