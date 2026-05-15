using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;

namespace MURO.Worker.Jobs;

/// <summary>
/// BBB canlı ders kayıtlarını işler:
/// 1. SessionRecording WHERE Status=Processing'leri tarar
/// 2. BbbService.GetRecordings() ile BBB'den video.mp4 URL'ini çeker
/// 3. MP4'ü indirir → HlsProcessingService ile 480p+720p HLS'e çevirir
/// 4. Thumbnail üretir
/// 5. MediaAsset ve SessionRecording'i Ready yapar
/// 6. Geçici dosyaları temizler
/// </summary>
public class BbbRecordingSyncJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BbbRecordingSyncJob> _logger;
    private readonly string _hlsOutputDir;
    private readonly string _tempDir;

    public BbbRecordingSyncJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<BbbRecordingSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hlsOutputDir = config["Storage:HlsOutputDir"] ?? Path.Combine("wwwroot", "hls");
        _tempDir = config["Storage:TempDir"] ?? Path.Combine(Path.GetTempPath(), "muro_processing");
        Directory.CreateDirectory(_hlsOutputDir);
        Directory.CreateDirectory(_tempDir);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BbbRecordingSyncJob başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRecordingsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BbbRecordingSyncJob döngüsünde beklenmedik hata.");
            }

            // Her 20 dakikada bir çalış (webhook birincil akışı handle eder, bu sadece fallback)
            await Task.Delay(TimeSpan.FromMinutes(20), stoppingToken);
        }
    }

    private async Task ProcessPendingRecordingsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db         = scope.ServiceProvider.GetRequiredService<MuroDbContext>();
        var bbbService = scope.ServiceProvider.GetRequiredService<IBbbService>();
        var hlsService = scope.ServiceProvider.GetRequiredService<IHlsProcessingService>();
        var httpClient = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("bbb-download");

        // Fix #12: Tek sorguda tüm Processing kayıtları çek; in-memory ayır
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var allProcessing = await db.SessionRecordings
            .Include(r => r.Session).ThenInclude(s => s.Course)
            .Include(r => r.MediaAsset)
            .Where(r => r.Status == MediaStatus.Processing)
            .ToListAsync(ct);

        // 24 saatten eski → Failed
        var stale = allProcessing.Where(r => r.CreatedAt <= cutoff).ToList();
        foreach (var s in stale)
        {
            s.Status = MediaStatus.Failed;
            _logger.LogWarning("SessionRecording süresi doldu → Failed. Id: {Id}", s.Id);
        }
        if (stale.Count > 0) await db.SaveChangesAsync(ct);

        // 24 saatten yeni ve MediaAsset henüz oluşturulmamış
        var pending = allProcessing
            .Where(r => r.CreatedAt > cutoff && r.MediaAsset == null)
            .ToList();

        if (pending.Count == 0)
        {
            _logger.LogDebug("İşlenecek BBB kaydı yok.");
            return;
        }

        _logger.LogInformation("{Count} BBB kaydı işlenecek.", pending.Count);

        foreach (var recording in pending)
        {
            await ProcessSingleRecordingAsync(db, bbbService, hlsService, httpClient, recording, ct);
        }
    }

    private async Task ProcessSingleRecordingAsync(
        MuroDbContext db,
        IBbbService bbbService,
        IHlsProcessingService hlsService,
        HttpClient httpClient,
        Domain.Entities.SessionRecording recording,
        CancellationToken ct)
    {
        var sessionId  = recording.SessionId;
        var meetingId  = recording.Session.BbbMeetingId;

        if (string.IsNullOrEmpty(meetingId))
        {
            _logger.LogWarning("BbbMeetingId boş. SessionId: {Id}", sessionId);
            return;
        }

        _logger.LogInformation("BBB kaydı işleniyor → SessionId: {SessionId} | MeetingId: {MeetingId}",
            sessionId, meetingId);

        // ── 1. BBB'den recordings listesini çek ──────────────────────────────
        var bbbRecordings = await bbbService.GetRecordingsAsync(meetingId);
        var videoRecord   = bbbRecordings.FirstOrDefault(r =>
            r.Status == "published" && r.PlaybackUrl != null);

        if (videoRecord == null)
        {
            _logger.LogInformation("BBB kaydı henüz hazır değil. MeetingId: {MeetingId}", meetingId);
            return; // 5 dk sonra tekrar dene
        }

        // ── 2. BBB presentation formatı — playback URL'ini doğrudan sakla ────
        // BBB varsayılan olarak "presentation" kaydeder (HTML+SVG+ses).
        // Bu bir MP4 URL'i değil, web oynatıcı URL'idir.
        // İframe ile embed edilir, HLS dönüşümüne gerek yok.
        var tenantId = recording.Session.Course.TenantId;
        var assetId  = Guid.NewGuid();

        var asset = new Domain.Entities.MediaAsset
        {
            Id            = assetId,
            TenantId      = tenantId,
            CourseId      = recording.Session.CourseId,
            Title         = $"{recording.Session.Title} — Kayıt",
            FilePath      = videoRecord.PlaybackUrl,   // BBB playback URL (iframe embed)
            HlsPath       = null,                       // HLS yok — iframe ile gösterilir
            ThumbnailPath = null,
            DurationSeconds = videoRecord.DurationSeconds,
            Status        = MediaStatus.Ready,
            CreatedAt     = DateTime.UtcNow
        };
        db.MediaAssets.Add(asset);

        // SessionRecording güncelle
        recording.MediaAssetId = assetId;
        recording.Status       = MediaStatus.Ready;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "BBB kaydı işlendi ✓ SessionId: {SessionId} | PlaybackUrl: {Url}",
            sessionId, videoRecord.PlaybackUrl);
    }
}
