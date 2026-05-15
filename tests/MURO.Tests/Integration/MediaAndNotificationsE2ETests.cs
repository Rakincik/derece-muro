using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Courses;
using MURO.Application.DTOs.Media;
using MURO.Application.DTOs.Notifications;
using MURO.Infrastructure.Persistence;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// Media + Bildirim E2E — Asset CRUD, video progress, podcast, bildirim yaşam
/// döngüsü (create → unread count → list → read → bulk), yetki kontrolleri.
/// </summary>
[Trait("Category", "E2E")]
public class MediaAndNotificationsE2ETests : IClassFixture<MuroTestFactory>, IAsyncLifetime
{
    private readonly MuroTestFactory _f;

    public MediaAndNotificationsE2ETests(MuroTestFactory factory) => _f = factory;
    public async Task InitializeAsync() => await _f.SeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> EnsureCourseExists(HttpClient admin)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MuroDbContext>();
        var existing = await db.Courses.FirstOrDefaultAsync(c => c.TenantId == _f.TestTenantId);
        if (existing != null) return existing.Id;

        var res = await admin.PostAsJsonAsync("/api/v1/courses",
            new CreateCourseRequest("Media E2E Kursu", null, null, "Online", null, null));
        return (await res.Content.ReadFromJsonAsync<CourseListDto>())!.Id;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 1. MEDIA ASSET TAM YAŞAM DÖNGÜSÜ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MediaAsset_FullCrudLifecycle()
    {
        var admin = _f.CreateAdminClient();
        var courseId = await EnsureCourseExists(admin);

        // Create
        var createRes = await admin.PostAsJsonAsync("/api/v1/media/assets",
            new CreateMediaAssetRequest("Fizik Video 1",
                "/videos/fizik1.mp4", courseId));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var asset = await createRes.Content.ReadFromJsonAsync<MediaAssetDto>();
        asset!.Title.Should().Be("Fizik Video 1");

        // Create ikinci asset
        var secondRes = await admin.PostAsJsonAsync("/api/v1/media/assets",
            new CreateMediaAssetRequest("Fizik Video 2",
                "/videos/fizik2.mp4", courseId));
        secondRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // List — ikisi de olmalı
        var listRes = await admin.GetAsync("/api/v1/media/assets");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listRes.Content.ReadFromJsonAsync<PagedResult<MediaAssetDto>>();
        list!.Items.Should().HaveCountGreaterThanOrEqualTo(2);

        // Update
        var updateRes = await admin.PutAsJsonAsync($"/api/v1/media/assets/{asset.Id}",
            new UpdateMediaAssetRequest("Fizik Video 1 (Güncel)", null, null, null, null));
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<MediaAssetDto>();
        updated!.Title.Should().Be("Fizik Video 1 (Güncel)");

        // Delete
        var delRes = await admin.DeleteAsync($"/api/v1/media/assets/{asset.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 2. VIDEO PROGRESS (Update + Get)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task VideoProgress_UpdateAndGet_ShouldTrack()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();
        var courseId = await EnsureCourseExists(admin);

        // Asset oluştur
        var assetRes = await admin.PostAsJsonAsync("/api/v1/media/assets",
            new CreateMediaAssetRequest("Progress Video",
                "/v/p.mp4", courseId));
        var asset = await assetRes.Content.ReadFromJsonAsync<MediaAssetDto>();

        // Progress güncelle — %50
        var updateRes = await student.PutAsJsonAsync(
            $"/api/v1/media/progress/{asset!.Id}",
            new UpdateVideoProgressRequest(300, 600, 300));
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var progress = await updateRes.Content.ReadFromJsonAsync<VideoProgressDto>();
        progress!.WatchedSeconds.Should().Be(300);

        // Progress güncelle — %92 → CompletedAt set edilmeli
        var completeRes = await student.PutAsJsonAsync(
            $"/api/v1/media/progress/{asset.Id}",
            new UpdateVideoProgressRequest(550, 600, 550));
        completeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await completeRes.Content.ReadFromJsonAsync<VideoProgressDto>();
        completed!.WatchedSeconds.Should().BeGreaterThanOrEqualTo(300); // Max mantığı
        completed.CompletedAt.Should().NotBeNull(); // %90+ → tamamlandı

        // Progress oku
        var getRes = await student.GetAsync($"/api/v1/media/progress/{asset.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3. BİLDİRİM TAM YAŞAM DÖNGÜSÜ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NotificationLifecycle_CreateListReadUnreadCount()
    {
        var admin = _f.CreateAdminClient();
        var student = _f.CreateStudentClient();

        // 1. Admin 3 bildirim oluşturur (öğrenciye)
        for (int i = 1; i <= 3; i++)
        {
            var r = await admin.PostAsJsonAsync("/api/v1/notifications",
                new CreateNotificationRequest(_f.TestStudentId,
                    $"Bildirim {i}", $"İçerik {i}", "Info", "InApp"));
            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 2. Öğrenci okunmamış sayısını kontrol eder
        var countRes = await student.GetAsync("/api/v1/notifications/unread-count");
        countRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var count = await countRes.Content.ReadFromJsonAsync<int>();
        count.Should().BeGreaterThanOrEqualTo(3);

        // 3. Öğrenci bildirim listesini alır
        var listRes = await student.GetAsync("/api/v1/notifications?page=1&pageSize=10");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listRes.Content.ReadFromJsonAsync<PagedResult<NotificationDto>>();
        list!.Items.Should().HaveCountGreaterThanOrEqualTo(3);

        // 4. İlk bildirimi okundu işaretle
        var firstId = list.Items[0].Id;
        var readRes = await student.PutAsync($"/api/v1/notifications/{firstId}/read", null);
        readRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. Okunmamış sayısı 1 azalmalı
        var newCountRes = await student.GetAsync("/api/v1/notifications/unread-count");
        var newCount = await newCountRes.Content.ReadFromJsonAsync<int>();
        newCount.Should().BeLessThan(count);

        // 6. Sadece okunmamış filtresi
        var unreadRes = await student.GetAsync("/api/v1/notifications?unreadOnly=true");
        unreadRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. Admin gönderilenleri görür
        var sentRes = await admin.GetAsync("/api/v1/notifications/admin/sent");
        sentRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4. PODCAST OLUŞTURMA
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Podcast_Create_ShouldReturn201()
    {
        var admin = _f.CreateAdminClient();

        var res = await admin.PostAsJsonAsync("/api/v1/media/podcasts",
            new CreatePodcastRequest("Test Podcast", "/audio/test.mp3", null));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var podcast = await res.Content.ReadFromJsonAsync<PodcastDto>();
        podcast!.Title.Should().Be("Test Podcast");

        // Podcast listesi
        var listRes = await admin.GetAsync("/api/v1/media/podcasts");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5. STUDENT MEDYA LİSTESİ
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task StudentMedia_ShouldAccessList()
    {
        var student = _f.CreateStudentClient();
        var res = await student.GetAsync("/api/v1/media");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 6. AUTH
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Media_NoAuth_ShouldReturn401()
    {
        (await _f.CreateAnonymousClient().GetAsync("/api/v1/media"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Notifications_NoAuth_ShouldReturn401()
    {
        (await _f.CreateAnonymousClient().GetAsync("/api/v1/notifications"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
