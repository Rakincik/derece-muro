using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Application.DTOs.Admin;

namespace MURO.Infrastructure.Services;

public class AdminJobsService : IAdminJobsService
{
    private readonly MuroDbContext _db;
    private readonly IBbbService _bbb;
    private readonly ILogger<AdminJobsService> _logger;
    private readonly IConfiguration _config;

    public AdminJobsService(MuroDbContext db, IBbbService bbb, ILogger<AdminJobsService> logger, IConfiguration config)
    {
        _db = db;
        _bbb = bbb;
        _logger = logger;
        _config = config;
    }

    private (int, object?) Ok(object? data = null) => (200, data);
    private (int, object?) NotFound(object? data = null) => (404, data);
    private (int, object?) BadRequest(object? data = null) => (400, data);
    private (int, object?) Conflict(object? data = null) => (409, data);

    public async Task<(int, object?)> GetJobStatus()
    {
        // MediaAsset pipeline (UploadProcessingJob)
        var mediaStats = await _db.MediaAssets
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        // SessionRecording pipeline (BbbRecordingSyncJob)
        var recordingStats = await _db.SessionRecordings
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        var totalFailed = mediaStats.Where(s => s.Status == "Failed").Sum(s => s.Count)
                        + recordingStats.Where(s => s.Status == "Failed").Sum(s => s.Count);

        return Ok(new
        {
            pipelines = new[]
            {
                new
                {
                    name = "UploadProcessingJob",
                    description = "Video yÃ¼kleme â†’ HLS dÃ¶nÃ¼ÅŸÃ¼m",
                    interval = "2 dakika",
                    stats = mediaStats,
                },
                new
                {
                    name = "BbbRecordingSyncJob",
                    description = "BBB kayÄ±t senkronizasyonu",
                    interval = "5 dakika",
                    stats = recordingStats,
                },
            },
            totalFailed,
            lastChecked = DateTime.UtcNow,
        });
    }

    /// <summary>Hata kuyruÄŸu â€” baÅŸarÄ±sÄ±z olan iÅŸ Ã¶geleri</summary>
    public async Task<(int, object?)> GetErrorQueue()
    {
        // BaÅŸarÄ±sÄ±z MediaAsset'ler
        var failedMedia = await _db.MediaAssets
            .Where(a => a.Status == MediaStatus.Failed)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new
            {
                id = a.Id,
                type = "MediaAsset",
                pipeline = "UploadProcessingJob",
                title = a.Title,
                tenantId = a.TenantId,
                createdAt = a.CreatedAt,
                filePath = a.FilePath,
            })
            .ToListAsync();

        // BaÅŸarÄ±sÄ±z SessionRecording'ler
        var failedRecordings = await _db.SessionRecordings
            .Include(r => r.Session).ThenInclude(s => s.Course)
            .Where(r => r.Status == MediaStatus.Failed)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new
            {
                id = r.Id,
                type = "SessionRecording",
                pipeline = "BbbRecordingSyncJob",
                title = r.Session.Title,
                tenantId = r.Session.Course.TenantId,
                createdAt = r.CreatedAt,
                filePath = (string?)null,
            })
            .ToListAsync();

        var all = failedMedia.Concat(failedRecordings)
            .OrderByDescending(x => x.createdAt)
            .ToList();

        return (200, new { items = all, totalCount = all.Count });
    }

    /// <summary>BaÅŸarÄ±sÄ±z Ã¶geyi tekrar kuyruÄŸa al (retry)</summary>
    public async Task<(int, object?)> RetryFailedItem(Guid id, RetryRequest request)
    {
        if (request.Type == "MediaAsset")
        {
            var asset = await _db.MediaAssets.FindAsync(id);
            if (asset == null) return (404, new { error = "MediaAsset not found" });
            if (asset.Status != MediaStatus.Failed)
                return (400, new { error = "Bu Ã¶ge Failed durumunda deÄŸil" });

            asset.Status = asset.FilePath != null ? MediaStatus.Uploading : MediaStatus.Processing;
            await _db.SaveChangesAsync();
            return (200, new { success = true, newStatus = asset.Status.ToString() });
        }
        else if (request.Type == "SessionRecording")
        {
            var recording = await _db.SessionRecordings.FindAsync(id);
            if (recording == null) return (404, new { error = "SessionRecording not found" });
            if (recording.Status != MediaStatus.Failed)
                return (400, new { error = "Bu Ã¶ge Failed durumunda deÄŸil" });

            recording.Status = MediaStatus.Processing;
            await _db.SaveChangesAsync();
            return (200, new { success = true, newStatus = "Processing" });
        }

        return (400, new { error = "GeÃ§ersiz type. 'MediaAsset' veya 'SessionRecording' olmalÄ±." });
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // BAKIM MODU
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>Kurum bakÄ±m modunu gÃ¼ncelle</summary>
}
