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
/// Sürekli-akan transcode kuyruğu.
/// Her encoder slotu bağımsız bir consumer'dır; bir iş bitince
/// hiç beklemeden kuyruktan yenisini atomik olarak çeker.
/// NVENC / QSV / CPU hatları birbirini ASLA beklemez.
/// </summary>
public class UploadProcessingJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UploadProcessingJob> _logger;
    private readonly string _hlsOutputDir;

    private readonly int _nvencSlots;
    private readonly int _qsvSlots;
    private readonly int _cpuSlots;

    private int _activeNvencCount = 0;
    private int _activeQsvCount = 0;

    private static readonly TimeSpan IdlePollDelay = TimeSpan.FromSeconds(3);

    public UploadProcessingJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<UploadProcessingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hlsOutputDir = config["Storage:HlsOutputDir"] ?? Path.Combine("wwwroot", "hls");
        Directory.CreateDirectory(_hlsOutputDir);

        _nvencSlots = config.GetValue<int>("Worker:NvencSlots", 3);
        _qsvSlots   = config.GetValue<int>("Worker:QsvSlots", 2);
        _cpuSlots   = config.GetValue<int>("Worker:CpuSlots", 6);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "UploadProcessingJob (sürekli akış) başlatıldı. NVENC={Nv} QSV={Qsv} CPU={Cpu} consumer.",
            _nvencSlots, _qsvSlots, _cpuSlots);

        var consumerTasks = new List<Task>();

        for (int i = 0; i < _nvencSlots; i++)
            consumerTasks.Add(RunConsumerLoopAsync("nvenc", i, stoppingToken));

        for (int i = 0; i < _qsvSlots; i++)
            consumerTasks.Add(RunConsumerLoopAsync("qsv", i, stoppingToken));

        for (int i = 0; i < _cpuSlots; i++)
            consumerTasks.Add(RunConsumerLoopAsync("cpu", i, stoppingToken));

        await Task.WhenAll(consumerTasks);
    }

    private async Task RunConsumerLoopAsync(string pipeline, int slotIndex, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Guid? claimedId = null;
            try
            {
                claimedId = await TryClaimNextAsync(pipeline, ct);

                if (claimedId == null)
                {
                    await Task.Delay(IdlePollDelay, ct);
                    continue;
                }

                if (pipeline == "nvenc") Interlocked.Increment(ref _activeNvencCount);
                else if (pipeline == "qsv") Interlocked.Increment(ref _activeQsvCount);

                try
                {
                    await ProcessClaimedAsync(claimedId.Value, pipeline, ct);
                }
                finally
                {
                    if (pipeline == "nvenc") Interlocked.Decrement(ref _activeNvencCount);
                    else if (pipeline == "qsv") Interlocked.Decrement(ref _activeQsvCount);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[{Pipeline}#{Slot}] consumer döngüsünde hata. AssetId: {Id}",
                    pipeline, slotIndex, claimedId);
                try { await Task.Delay(TimeSpan.FromSeconds(1), ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task<Guid?> TryClaimNextAsync(string pipeline, CancellationToken ct)
    {
        // ÖNCELİKLENDİRME MANTIĞI:
        // Eğer QSV isek ve NVENC'in hala boş slotu varsa (tam kapasite çalışmıyorsa), videoyu alma, NVENC kapsın.
        if (pipeline == "qsv" && _nvencSlots > 0 && Volatile.Read(ref _activeNvencCount) < _nvencSlots) return null;
        
        // Eğer CPU isek ve NVENC veya QSV'nin boş slotu varsa, videoyu alma, güçlü kartlar kapsın.
        if (pipeline == "cpu" && (
            (_nvencSlots > 0 && Volatile.Read(ref _activeNvencCount) < _nvencSlots) || 
            (_qsvSlots > 0 && Volatile.Read(ref _activeQsvCount) < _qsvSlots))) 
            return null;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MuroDbContext>();

        var candidateId = await db.MediaAssets
            .Where(a => a.Status == MediaStatus.Uploading
                     && a.FilePath != null
                     && a.HlsPath == null)
            .OrderBy(a => a.CreatedAt)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);

        if (candidateId == null)
            return null;

        var affected = await db.MediaAssets
            .Where(a => a.Id == candidateId.Value && a.Status == MediaStatus.Uploading)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, MediaStatus.Processing), ct);

        return affected == 1 ? candidateId : null;
    }

    private async Task ProcessClaimedAsync(Guid assetId, string pipeline, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<MuroDbContext>();
        var hls         = scope.ServiceProvider.GetRequiredService<IHlsProcessingService>();
        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        var asset = await db.MediaAssets.FindAsync(new object[] { assetId }, ct);
        if (asset == null) return;

        string localMp4Path = asset.FilePath!;
        bool isDownloaded = false;

        if (asset.FilePath!.Contains("/api/v1/uploads/"))
        {
            var uri = new Uri(asset.FilePath);
            var fileName = Path.GetFileName(uri.LocalPath);
            localMp4Path = Path.Combine("wwwroot", "uploads", fileName);
            _logger.LogInformation("[{Pipeline}] Lokal dosya: {Path}", pipeline, localMp4Path);
        }
        else if (asset.FilePath!.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 asset.FilePath!.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[{Pipeline}] Video indiriliyor: {Url}", pipeline, asset.FilePath);
            var client = httpFactory.CreateClient("bbb-download");
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
                _logger.LogInformation("[{Pipeline}] İndirildi: {Path}", pipeline, localMp4Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Pipeline}] İndirme hatası: {Url}", pipeline, asset.FilePath);
                await MarkFailedAsync(db, asset, ct);
                return;
            }
        }
        else if (!File.Exists(localMp4Path))
        {
            _logger.LogWarning("[{Pipeline}] Dosya yok: {Path} | {Id}", pipeline, localMp4Path, asset.Id);
            await MarkFailedAsync(db, asset, ct);
            return;
        }

        _logger.LogInformation("[{Pipeline}] İşleniyor → {Id} | {Path}", pipeline, asset.Id, localMp4Path);

        var outDir = Path.Combine(_hlsOutputDir, asset.TenantId.ToString());
        var result = await hls.ProcessAsync(asset.Id, localMp4Path, outDir, pipeline, ct);

        if (!result.Success)
        {
            _logger.LogError("[{Pipeline}] HLS başarısız → {Id} | {Err}", pipeline, asset.Id, result.ErrorMessage);
            await MarkFailedAsync(db, asset, ct);
            if (isDownloaded && File.Exists(localMp4Path)) File.Delete(localMp4Path);
            return;
        }

        try
        {
            if (File.Exists(localMp4Path)) File.Delete(localMp4Path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Pipeline}] Temp silinemedi: {Path}", pipeline, localMp4Path);
        }

        asset.FilePath      = null;
        asset.HlsPath       = $"/hls/{asset.TenantId}/{asset.Id}/master.m3u8";
        asset.ThumbnailPath = string.IsNullOrEmpty(result.ThumbnailPath) ? null : $"/hls/{asset.TenantId}/{asset.Id}/thumbnail.jpg";
        if (result.DurationSeconds.HasValue) asset.DurationSeconds = result.DurationSeconds;
        asset.Status = MediaStatus.Ready;

        await UpdateRecordingStatusAsync(db, asset.Id, MediaStatus.Ready, ct);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("[{Pipeline}] Tamamlandı ✓ {Id}", pipeline, asset.Id);
    }

    private async Task MarkFailedAsync(MuroDbContext db, Domain.Entities.MediaAsset asset, CancellationToken ct)
    {
        asset.Status = MediaStatus.Failed;
        await UpdateRecordingStatusAsync(db, asset.Id, MediaStatus.Failed, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task UpdateRecordingStatusAsync(MuroDbContext db, Guid mediaAssetId, MediaStatus newStatus, CancellationToken ct)
    {
        var recording = await db.SessionRecordings
            .FirstOrDefaultAsync(r => r.MediaAssetId == mediaAssetId, ct);
        if (recording != null)
            recording.Status = newStatus;
    }
}
