using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Media;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class MediaService : IMediaService
{
    private readonly MuroDbContext _context;
    private readonly IGroupAccessService _groupAccess;
    private readonly ICacheService _cache;
    private readonly string _hlsOutputDir;
    private readonly ILogger<MediaService> _logger;

    public MediaService(MuroDbContext context, IGroupAccessService groupAccess, ICacheService cache, IConfiguration config, ILogger<MediaService> logger)
    {
        _context = context;
        _groupAccess = groupAccess;
        _logger = logger;
        _hlsOutputDir = config["Storage:HlsOutputDir"] ?? System.IO.Path.Combine("wwwroot", "hls");
        _cache = cache;
    }

    public async Task<PagedResult<MediaAssetDto>> GetAssetsAsync(
        int page, int pageSize, Guid? courseId, string? search = null, Guid? userId = null, Guid? folderId = null, bool excludeRecordings = false)
    {
        var query = _context.MediaAssets.AsNoTracking().Where(m => true);

        // Student grup filtresi: userId verilmişse erişilebilir kurslarla sınırla
        if (userId.HasValue)
        {
            var accessibleIds = await _groupAccess.GetAccessibleCourseIdsAsync(userId.Value);
            query = query.Where(m => (m.CourseId.HasValue && accessibleIds.Contains(m.CourseId.Value)) 
                                  || m.CourseMedias.Any(cm => accessibleIds.Contains(cm.CourseId)));
        }

        if (courseId.HasValue) query = query.Where(m => m.CourseMedias.Any(cm => cm.CourseId == courseId));
        if (folderId.HasValue) query = query.Where(m => m.FolderId == folderId);
        if (excludeRecordings) query = query.Where(m => !_context.SessionRecordings.Any(sr => sr.MediaAssetId == m.Id) && !m.Title.EndsWith("Kayıt") && (m.FilePath == null || !m.FilePath.Contains("/playback/presentation/")));
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            var trCulture = new CultureInfo("tr-TR");
            var s = search.ToLower(trCulture)
                .Replace("ı", "i")
                .Replace("i", "i")
                .Replace("ö", "o")
                .Replace("ü", "u")
                .Replace("ş", "s")
                .Replace("ğ", "g")
                .Replace("ç", "c");

            query = query.Where(m => 
                m.Title.ToLower()
                    .Replace("ı", "i")
                    .Replace("İ", "i")
                    .Replace("ö", "o")
                    .Replace("ü", "u")
                    .Replace("ş", "s")
                    .Replace("ğ", "g")
                    .Replace("ç", "c")
                    .Contains(s)
            );
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        List<MediaAssetDto> items;

        if (courseId.HasValue)
        {
            items = await query
                .Select(m => new
                {
                    Asset = m,
                    Order = m.CourseMedias.Where(cm => cm.CourseId == courseId.Value).Select(cm => (int?)cm.OrderIndex).FirstOrDefault() ?? 9999
                })
                .OrderBy(x => x.Order)
                .ThenByDescending(x => x.Asset.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new MediaAssetDto(x.Asset.Id, x.Asset.Title, x.Asset.FilePath, x.Asset.HlsPath, x.Asset.ThumbnailPath,
                    x.Asset.DurationSeconds, x.Asset.Status.ToString(), courseId,
                    null, x.Asset.FolderId, x.Asset.CreatedAt, x.Asset.Tags))
                .ToListAsync();
        }
        else
        {
            items = await query.OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(m => new MediaAssetDto(m.Id, m.Title, m.FilePath, m.HlsPath, m.ThumbnailPath,
                    m.DurationSeconds, m.Status.ToString(), m.CourseId,
                    null, m.FolderId, m.CreatedAt, m.Tags))
                .ToListAsync();
        }

        return new PagedResult<MediaAssetDto>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<MediaAssetDto> GetAssetByIdAsync(Guid assetId, Guid? userId = null)
    {
        var a = await _context.MediaAssets.AsNoTracking()
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == assetId )
            ?? throw new KeyNotFoundException("Medya bulunamad\u0131.");

        var cmIds = await _context.CourseMedias.Where(cm => cm.MediaAssetId == assetId).Select(cm => cm.CourseId).ToListAsync();
        
        // Student erişim kontrolü: kursa grubu üzerinden erişim hakkı var mı?
        if (userId.HasValue)
        {
            var courseIds = new List<Guid>();
            if (a.CourseId.HasValue) courseIds.Add(a.CourseId.Value);
            courseIds.AddRange(cmIds);

            bool hasAccess = false;
            foreach(var cid in courseIds) {
                if (await _groupAccess.CanAccessCourseAsync(userId.Value, cid)) {
                    hasAccess = true;
                    break;
                }
            }
            if (!hasAccess && courseIds.Any())
                throw new UnauthorizedAccessException("Bu videoya erişim yetkiniz yok.");
        }

        Guid? resolvedCourseId = a.CourseId;
        if (!resolvedCourseId.HasValue && cmIds.Any())
        {
            resolvedCourseId = cmIds.First();
        }

        return new MediaAssetDto(a.Id, a.Title, a.FilePath, a.HlsPath, a.ThumbnailPath,
            a.DurationSeconds, a.Status.ToString(), resolvedCourseId,
            a.Course != null ? a.Course.Title : null, a.FolderId, a.CreatedAt, a.Tags);
    }

    public async Task<MediaAssetDto> CreateAssetAsync(CreateMediaAssetRequest request)
    {
        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(), Title = request.Title, FilePath = request.FilePath, CourseId = request.CourseId,
            DurationSeconds = request.DurationSeconds,
            FolderId = request.FolderId, Tags = request.Tags
        };
        _context.MediaAssets.Add(asset);
        
        if (request.CourseId.HasValue)
        {
            var hasManualSort = await _context.CourseMedias
                .AnyAsync(cm => cm.CourseId == request.CourseId.Value && cm.OrderIndex >= 0);

            int orderIndex = -1;
            if (hasManualSort)
            {
                var maxOrder = await _context.CourseMedias
                    .Where(cm => cm.CourseId == request.CourseId.Value)
                    .MaxAsync(cm => (int?)cm.OrderIndex) ?? -1;
                orderIndex = maxOrder + 1;
            }

            _context.CourseMedias.Add(new CourseMedia
            {
                CourseId = request.CourseId.Value,
                MediaAssetId = asset.Id,
                OrderIndex = orderIndex
            });
        }
        
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"media:");
        return new MediaAssetDto(asset.Id, asset.Title, asset.FilePath, null, null, null,
            asset.Status.ToString(), asset.CourseId, null, asset.FolderId, asset.CreatedAt, asset.Tags);
    }

    public async Task<MediaAssetDto> UpdateAssetAsync(Guid assetId, UpdateMediaAssetRequest request)
    {
        var a = await _context.MediaAssets.FirstOrDefaultAsync(m => m.Id == assetId )
            ?? throw new KeyNotFoundException("Medya bulunamadı.");
        if (request.Title != null) a.Title = request.Title;
        if (request.HlsPath != null) a.HlsPath = request.HlsPath;
        if (request.ThumbnailPath != null) a.ThumbnailPath = request.ThumbnailPath;
        if (request.DurationSeconds.HasValue) a.DurationSeconds = request.DurationSeconds;
        if (request.Status != null && Enum.TryParse<MediaStatus>(request.Status, true, out var st)) a.Status = st;
        if (request.FolderId.HasValue) a.FolderId = request.FolderId;
        if (request.Tags != null) a.Tags = request.Tags;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"media:");
        return new MediaAssetDto(a.Id, a.Title, a.FilePath, a.HlsPath, a.ThumbnailPath,
            a.DurationSeconds, a.Status.ToString(), a.CourseId, null, a.FolderId, a.CreatedAt, a.Tags);
    }

    public async Task DeleteAssetAsync(Guid assetId)
    {
        var a = await _context.MediaAssets.FirstOrDefaultAsync(m => m.Id == assetId )
            ?? throw new KeyNotFoundException("Medya bulunamadı.");

        try
        {
            var hlsDir = Path.Combine(_hlsOutputDir, Guid.Empty.ToString(), assetId.ToString());
            if (Directory.Exists(hlsDir))
            {
                try
                {
                    Directory.Delete(hlsDir, recursive: true);
                }
                catch (IOException)
                {
                    // Linux ortamında ffmpeg dosyaya yazarken Directory.Delete başarısız olabilir.
                    // Zorla silmek için rm -rf kullanıyoruz. Bu aynı zamanda ffmpeg'in hata verip kapanmasını sağlar.
                    try
                    {
                        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "rm",
                            Arguments = $"-rf \"{hlsDir}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        process?.WaitForExit(2000);
                    }
                    catch { /* ignore */ }
                }
                _logger.LogInformation("HLS klasörü silindi: {Dir}", hlsDir);
            }

            if (!string.IsNullOrEmpty(a.FilePath) && a.FilePath.Contains("/uploads/"))
            {
                var fileName = Path.GetFileName(a.FilePath);
                var uploadPath = Path.Combine(_hlsOutputDir, "..", "uploads", fileName);
                if (File.Exists(uploadPath)) File.Delete(uploadPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Disk dosyalari silinirken hata (DB silme devam edecek): {AssetId}", assetId);
        }

        _context.MediaAssets.Remove(a);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"media:");
    }

    public async Task<List<Guid>> GetAssignedCourseIdsAsync(Guid mediaAssetId)
    {
        return await _context.CourseMedias
            .Where(cm => cm.MediaAssetId == mediaAssetId )
            .Select(cm => cm.CourseId)
            .ToListAsync();
    }

    public async Task<VideoProgressDto> GetProgressAsync(Guid userId, Guid mediaAssetId)
    {
        var p = await _context.VideoProgresses.AsNoTracking()
            .Include(vp => vp.MediaAsset)
            .FirstOrDefaultAsync(vp => vp.UserId == userId && vp.MediaAssetId == mediaAssetId);

        if (p == null) return new VideoProgressDto(Guid.Empty, mediaAssetId, "", 0, 0, 0, 0, null, DateTime.UtcNow);

        var pct = p.TotalSeconds > 0 ? Math.Round(p.WatchedSeconds / (double)p.TotalSeconds * 100, 1) : 0;
        return new VideoProgressDto(p.Id, p.MediaAssetId, p.MediaAsset.Title,
            p.WatchedSeconds, p.TotalSeconds, p.LastPosition, pct, p.CompletedAt, p.UpdatedAt);
    }

    public async Task<VideoProgressDto> UpdateProgressAsync(Guid userId, Guid mediaAssetId, UpdateVideoProgressRequest request)
    {
        var p = await _context.VideoProgresses
            .Include(vp => vp.MediaAsset)
            .FirstOrDefaultAsync(vp => vp.UserId == userId && vp.MediaAssetId == mediaAssetId);

        if (p == null)
        {
            p = new VideoProgress
            {
                Id = Guid.NewGuid(), UserId = userId, MediaAssetId = mediaAssetId,
                WatchedSeconds = request.WatchedSeconds, TotalSeconds = request.TotalSeconds,
                LastPosition = request.LastPosition
            };
            _context.VideoProgresses.Add(p);
        }
        else
        {
            p.WatchedSeconds = Math.Max(p.WatchedSeconds, request.WatchedSeconds);
            p.TotalSeconds = request.TotalSeconds;
            p.LastPosition = request.LastPosition;
            p.UpdatedAt = DateTime.UtcNow;
        }

        if (p.TotalSeconds > 0 && p.WatchedSeconds >= p.TotalSeconds * 0.9)
            p.CompletedAt ??= DateTime.UtcNow;

        var title = p.MediaAsset?.Title ?? await _context.MediaAssets.Where(m => m.Id == mediaAssetId).Select(m => m.Title).FirstOrDefaultAsync() ?? "Bilinmeyen Video";
        p.AuditDisplayName = title;

        await _context.SaveChangesAsync();

        var pct = p.TotalSeconds > 0 ? Math.Round(p.WatchedSeconds / (double)p.TotalSeconds * 100, 1) : 0;
        return new VideoProgressDto(p.Id, p.MediaAssetId, title, p.WatchedSeconds, p.TotalSeconds,
            p.LastPosition, pct, p.CompletedAt, p.UpdatedAt);
    }

    public async Task<PagedResult<PodcastDto>> GetPodcastsAsync(int page, int pageSize)
    {
        var query = _context.Podcasts.AsNoTracking().Where(p => true);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(p => p.Course)
            .Select(p => new PodcastDto(p.Id, p.Title, p.AudioFilePath, p.DurationSeconds,
                p.Status.ToString(), p.CourseId, p.Course != null ? p.Course.Title : null, p.CreatedAt))
            .ToListAsync();

        return new PagedResult<PodcastDto>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<PodcastDto> CreatePodcastAsync(CreatePodcastRequest request)
    {
        var pod = new Podcast
        {
            Id = Guid.NewGuid(), Title = request.Title, AudioFilePath = request.AudioUrl, CourseId = request.CourseId
        };
        _context.Podcasts.Add(pod);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"podcasts:");
        return new PodcastDto(pod.Id, pod.Title, pod.AudioFilePath, null, pod.Status.ToString(),
            pod.CourseId, null, pod.CreatedAt);
    }
}
