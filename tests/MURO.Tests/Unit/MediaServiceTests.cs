using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MURO.Application.DTOs.Media;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// MediaService için unit testler — Asset CRUD, video ilerleme kaydı,
/// grup erişim kontrolü, arama, sayfalama, podcast yönetimi.
/// </summary>
public class MediaServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly MediaService _service;
    private readonly Mock<IGroupAccessService> _groupAccessMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public MediaServiceTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _groupAccessMock = new Mock<IGroupAccessService>();
        _service = new MediaService(_db, _groupAccessMock.Object, new MURO.Tests.Helpers.DummyCacheService());

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test", Code = "media" });
        _db.Users.Add(new User { Id = _userId, Email = "u@t.com", FirstName = "Ali", LastName = "Y", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Fizik 101", IsPublished = true });
        _db.SaveChanges();
    }

    // ════════════════════════════════════════════════════════════════════════
    // CREATE ASSET
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsset_ShouldSaveToDb()
    {
        var result = await _service.CreateAssetAsync(_tenantId,
            new CreateMediaAssetRequest("Video 1", "/uploads/v1.mp4", _courseId));

        result.Title.Should().Be("Video 1");
        result.Status.Should().Be("Uploading");
        (await _db.MediaAssets.CountAsync()).Should().Be(1);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GET ASSET BY ID
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAssetById_ShouldReturn()
    {
        var created = await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("V1", "/v.mp4", _courseId));
        var result = await _service.GetAssetByIdAsync(_tenantId, created.Id);
        result.Title.Should().Be("V1");
    }

    [Fact]
    public async Task GetAssetById_NonExistent_ShouldThrow()
    {
        var act = () => _service.GetAssetByIdAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetAssetById_StudentWithAccess_ShouldReturn()
    {
        var created = await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("V1", "/v.mp4", _courseId));
        _groupAccessMock.Setup(g => g.CanAccessCourseAsync(_tenantId, _userId, _courseId)).ReturnsAsync(true);

        var result = await _service.GetAssetByIdAsync(_tenantId, created.Id, _userId);
        result.Title.Should().Be("V1");
    }

    [Fact]
    public async Task GetAssetById_StudentWithoutAccess_ShouldThrow()
    {
        var created = await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("V1", "/v.mp4", _courseId));
        _groupAccessMock.Setup(g => g.CanAccessCourseAsync(_tenantId, _userId, _courseId)).ReturnsAsync(false);

        var act = () => _service.GetAssetByIdAsync(_tenantId, created.Id, _userId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*erişim yetkiniz yok*");
    }

    // ════════════════════════════════════════════════════════════════════════
    // UPDATE ASSET
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateAsset_ShouldModifyFields()
    {
        var created = await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("V1", "/v.mp4", null));
        var updated = await _service.UpdateAssetAsync(_tenantId, created.Id,
            new UpdateMediaAssetRequest("Yeni Başlık", "/hls/v1/index.m3u8", "/thumb/v1.jpg", 3600, "Ready"));

        updated.Title.Should().Be("Yeni Başlık");
        updated.HlsPath.Should().Be("/hls/v1/index.m3u8");
        updated.DurationSeconds.Should().Be(3600);
        updated.Status.Should().Be("Ready");
    }

    [Fact]
    public async Task UpdateAsset_NonExistent_ShouldThrow()
    {
        var act = () => _service.UpdateAssetAsync(_tenantId, Guid.NewGuid(), new UpdateMediaAssetRequest("X", null, null, null, null));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ════════════════════════════════════════════════════════════════════════
    // DELETE ASSET
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteAsset_ShouldRemove()
    {
        var created = await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("V1", "/v.mp4", null));
        await _service.DeleteAssetAsync(_tenantId, created.Id);
        (await _db.MediaAssets.AnyAsync(m => m.Id == created.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsset_NonExistent_ShouldThrow()
    {
        var act = () => _service.DeleteAssetAsync(_tenantId, Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ════════════════════════════════════════════════════════════════════════
    // GET ASSETS (Paging, Search, CourseFilter)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAssets_Paging_ShouldWork()
    {
        for (int i = 0; i < 15; i++)
            await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest($"Video {i}", null, null));

        var p1 = await _service.GetAssetsAsync(_tenantId, 1, 10, null);
        var p2 = await _service.GetAssetsAsync(_tenantId, 2, 10, null);

        p1.Items.Should().HaveCount(10);
        p2.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAssets_SearchByTitle_ShouldFilter()
    {
        await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("Matematik Video", null, null));
        await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("Fizik Video", null, null));

        var result = await _service.GetAssetsAsync(_tenantId, 1, 10, null, "fizik");
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Fizik Video");
    }

    [Fact]
    public async Task GetAssets_FilterByCourse_ShouldFilter()
    {
        await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("Kursa ait", null, _courseId));
        await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("Genel", null, null));

        var result = await _service.GetAssetsAsync(_tenantId, 1, 10, _courseId);
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Kursa ait");
    }

    [Fact]
    public async Task GetAssets_StudentFilter_ShouldOnlyShowAccessible()
    {
        await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("Erişilebilir", null, _courseId));
        var otherCourseId = Guid.NewGuid();
        _db.Courses.Add(new Course { Id = otherCourseId, TenantId = _tenantId, Title = "Gizli Kurs" });
        await _db.SaveChangesAsync();
        await _service.CreateAssetAsync(_tenantId, new CreateMediaAssetRequest("Erişilemez", null, otherCourseId));

        _groupAccessMock.Setup(g => g.GetAccessibleCourseIdsAsync(_tenantId, _userId))
            .ReturnsAsync(new HashSet<Guid> { _courseId });

        var result = await _service.GetAssetsAsync(_tenantId, 1, 10, null, null, _userId);
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Erişilebilir");
    }

    // ════════════════════════════════════════════════════════════════════════
    // VIDEO PROGRESS
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetProgress_NoRecord_ShouldReturnDefault()
    {
        var mediaId = Guid.NewGuid();
        _db.MediaAssets.Add(new MediaAsset { Id = mediaId, Title = "V", TenantId = _tenantId, Status = MediaStatus.Ready });
        await _db.SaveChangesAsync();

        var result = await _service.GetProgressAsync(_userId, mediaId);
        result.WatchedSeconds.Should().Be(0);
    }

    [Fact]
    public async Task UpdateProgress_NewRecord_ShouldCreate()
    {
        var mediaId = Guid.NewGuid();
        _db.MediaAssets.Add(new MediaAsset { Id = mediaId, Title = "Ders1", TenantId = _tenantId, Status = MediaStatus.Ready });
        await _db.SaveChangesAsync();

        var result = await _service.UpdateProgressAsync(_userId, mediaId,
            new UpdateVideoProgressRequest(120, 600, 120));

        result.WatchedSeconds.Should().Be(120);
        result.PercentComplete.Should().Be(20);
    }

    [Fact]
    public async Task UpdateProgress_ExistingRecord_ShouldUseMax()
    {
        var mediaId = Guid.NewGuid();
        _db.MediaAssets.Add(new MediaAsset { Id = mediaId, Title = "V", TenantId = _tenantId, Status = MediaStatus.Ready });
        await _db.SaveChangesAsync();

        await _service.UpdateProgressAsync(_userId, mediaId, new UpdateVideoProgressRequest(300, 600, 300));
        // Daha düşük süre gelse bile max korunmalı
        var result = await _service.UpdateProgressAsync(_userId, mediaId, new UpdateVideoProgressRequest(100, 600, 100));

        result.WatchedSeconds.Should().Be(300); // Math.Max(300, 100)
    }

    [Fact]
    public async Task UpdateProgress_90Percent_ShouldComplete()
    {
        var mediaId = Guid.NewGuid();
        _db.MediaAssets.Add(new MediaAsset { Id = mediaId, Title = "V", TenantId = _tenantId, Status = MediaStatus.Ready });
        await _db.SaveChangesAsync();

        var result = await _service.UpdateProgressAsync(_userId, mediaId, new UpdateVideoProgressRequest(540, 600, 540));
        result.CompletedAt.Should().NotBeNull();
    }

    public void Dispose() => _db.Dispose();
}
