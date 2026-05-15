using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;

namespace MURO.Worker.Jobs;

/// <summary>
/// Admin panelden yüklenen MP4 videoları işler:
/// 1. MediaAsset WHERE Status=Uploading AND FilePath!=null tarar
/// 2. HlsProcessingService ile 480p+720p HLS'e çevirir
/// 3. Thumbnail üretir
/// 4. Orijinal MP4'ü siler
/// 5. MediaAsset.HlsPath doldurur, Status = Ready yapar
/// </summary>
public class UploadProcessingJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UploadProcessingJob> _logger;
    private readonly string _hlsOutputDir;

    public UploadProcessingJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<UploadProcessingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hlsOutputDir = config["Storage:HlsOutputDir"] ?? Path.Combine("wwwroot", "hls");
        Directory.CreateDirectory(_hlsOutputDir);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UploadProcessingJob başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingUploadsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadProcessingJob döngüsünde beklenmedik hata.");
            }

            // Her 15 saniyede bir kontrol et (test için hızlandırıldı, canlıda 2 dk olabilir)
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task ProcessPendingUploadsAsync(CancellationToken ct)
    {
        using var scope  = _scopeFactory.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<MuroDbContext>();
        var hlsService   = scope.ServiceProvider.GetRequiredService<IHlsProcessingService>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        // Yüklendi ama henüz HLS'e çevrilmemiş varlıklar
        var pending = await db.MediaAssets
            .Where(a =>
                a.Status == MediaStatus.Uploading &&
                a.FilePath != null &&
                a.HlsPath == null)
            .ToListAsync(ct);

        if (!pending.Any())
        {
            _logger.LogDebug("İşlenecek yükleme yok.");
            return;
        }

        _logger.LogInformation("{Count} video işlenecek.", pending.Count);

        foreach (var asset in pending)
        {
            await ProcessSingleUploadAsync(db, hlsService, httpClientFactory, asset, ct);
        }
    }

    private async Task ProcessSingleUploadAsync(
        MuroDbContext db,
        IHlsProcessingService hlsService,
        IHttpClientFactory httpClientFactory,
        Domain.Entities.MediaAsset asset,
        CancellationToken ct)
    {
        string localMp4Path = asset.FilePath!;
        bool isDownloaded = false;

        if (asset.FilePath!.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            asset.FilePath!.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("URL tespit edildi, video indiriliyor: {Url}", asset.FilePath);
            var client = httpClientFactory.CreateClient("bbb-download");
            
            try
            {
                var response = await client.GetAsync(asset.FilePath, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var tempDir = Path.Combine(Path.GetTempPath(), "muro_processing");
                Directory.CreateDirectory(tempDir);
                
                localMp4Path = Path.Combine(tempDir, $"{Guid.NewGuid():N}.mp4");
                
                using var fs = new FileStream(localMp4Path, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs, ct);
                
                isDownloaded = true;
                _logger.LogInformation("Video indirildi: {Path}", localMp4Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Video indirilirken hata oluştu: {Url}", asset.FilePath);
                asset.Status = MediaStatus.Failed;
                await UpdateRecordingStatusAsync(db, asset.Id, MediaStatus.Failed, ct);
                await db.SaveChangesAsync(ct);
                return;
            }
        }
        else if (!File.Exists(localMp4Path))
        {
            _logger.LogWarning("Dosya bulunamadı: {Path} | AssetId: {Id}", localMp4Path, asset.Id);
            asset.Status = MediaStatus.Failed;
            await UpdateRecordingStatusAsync(db, asset.Id, MediaStatus.Failed, ct);
            await db.SaveChangesAsync(ct);
            return;
        }

        _logger.LogInformation("Video işleniyor → AssetId: {Id} | Kaynak: {Path}", asset.Id, localMp4Path);

        // Processing durumuna al (çift işlem önlemi)
        asset.Status = MediaStatus.Processing;
        await db.SaveChangesAsync(ct);

        var outDir = Path.Combine(_hlsOutputDir, asset.TenantId.ToString());
        var result = await hlsService.ProcessAsync(asset.Id, localMp4Path, outDir, ct);

        if (!result.Success)
        {
            _logger.LogError("HLS dönüşümü başarısız. AssetId: {Id} | Hata: {Err}", asset.Id, result.ErrorMessage);
            asset.Status = MediaStatus.Failed;
            await UpdateRecordingStatusAsync(db, asset.Id, MediaStatus.Failed, ct);
            await db.SaveChangesAsync(ct);
            if (isDownloaded) File.Delete(localMp4Path);
            return;
        }

        // İndirilen temp dosyasını veya yerel dosyayı sil (storage tasarrufu)
        try
        {
            File.Delete(localMp4Path);
            _logger.LogInformation("Orijinal/Temp MP4 silindi: {Path}", localMp4Path);
            
            // Eğer yerel dosya ise ve API'ın yüklediği upload klasöründeyse o asıl dosyayı da silelim (API URL'sinden)
            // Biz indirdik ama orijinal sunucuda duruyor olabilir. 
            // Şimdilik sadece kullandığımız dosyayı siliyoruz.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Orijinal/Temp dosya silinemedi: {Path}", localMp4Path);
        }

        // MediaAsset güncelle
        asset.FilePath      = null;                      // Orijinal kaynak bırakılabilir veya null yapılabilir, mevcut kodda null
        asset.HlsPath       = $"/hls/{asset.TenantId}/{asset.Id}/master.m3u8";
        asset.ThumbnailPath = string.IsNullOrEmpty(result.ThumbnailPath) ? null : $"/hls/{asset.TenantId}/{asset.Id}/thumbnail.jpg";
        asset.Status        = MediaStatus.Ready;

        await UpdateRecordingStatusAsync(db, asset.Id, MediaStatus.Ready, ct);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Video işlendi ✓ AssetId: {Id} | HlsPath: {Path}",
            asset.Id, result.MasterPlaylistPath);
    }

    private async Task UpdateRecordingStatusAsync(MuroDbContext db, Guid mediaAssetId, MediaStatus newStatus, CancellationToken ct)
    {
        var recording = await db.SessionRecordings
            .FirstOrDefaultAsync(r => r.MediaAssetId == mediaAssetId, ct);
            
        if (recording != null)
        {
            recording.Status = newStatus;
        }
    }
}
