using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Videos;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

// ═══════════════════════════════════════════════════════════════
// VIDEO NOTES SERVICE
// ═══════════════════════════════════════════════════════════════

public class VideoNoteService : IVideoNoteService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public VideoNoteService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<VideoNoteDto>> GetMyNotesAsync(Guid userId, Guid mediaAssetId)
    {
        var cacheKey = $"videonotes:{userId}:{mediaAssetId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await _context.VideoNotes
                .AsNoTracking()
                .Where(vn => vn.UserId == userId && vn.MediaAssetId == mediaAssetId)
                .OrderBy(vn => vn.TimestampSeconds)
                .Select(vn => new VideoNoteDto(
                    vn.Id, vn.MediaAssetId, vn.TimestampSeconds,
                    FormatTimestamp(vn.TimestampSeconds),
                    vn.Text, vn.CreatedAt))
                .ToListAsync();
        }, TimeSpan.FromMinutes(3));
    }

    public async Task<VideoNoteDto> AddNoteAsync(Guid userId, Guid mediaAssetId, CreateVideoNoteRequest request)
    {
        var assetExists = await _context.MediaAssets.AnyAsync(m => m.Id == mediaAssetId);
        if (!assetExists)
        {
            var cm = await _context.CourseMedias.Include(c => c.Session).FirstOrDefaultAsync(c => c.Id == mediaAssetId);
            if (cm != null)
            {
                _context.MediaAssets.Add(new MediaAsset
                {
                    Id = mediaAssetId,
                    CourseId = cm.CourseId,
                    Title = cm.Session?.Title ?? "Ders Kaydı",
                    Status = MURO.Domain.Enums.MediaStatus.Ready
                });
            }
            else
            {
                var sr = await _context.SessionRecordings.Include(r => r.Session).FirstOrDefaultAsync(r => r.Id == mediaAssetId);
                if (sr != null)
                {
                    _context.MediaAssets.Add(new MediaAsset
                    {
                        Id = mediaAssetId,
                        CourseId = sr.Session.CourseId,
                        Title = sr.Session.Title,
                        Status = MURO.Domain.Enums.MediaStatus.Ready
                    });
                }
            }
        }

        var note = new VideoNote
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MediaAssetId = mediaAssetId,
            TimestampSeconds = request.TimestampSeconds,
            Text = request.Text
        };
        _context.VideoNotes.Add(note);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"videonotes:{userId}:{mediaAssetId}");

        return new VideoNoteDto(note.Id, note.MediaAssetId, note.TimestampSeconds,
            FormatTimestamp(note.TimestampSeconds), note.Text, note.CreatedAt);
    }

    public async Task<VideoNoteDto> UpdateNoteAsync(Guid userId, Guid noteId, UpdateVideoNoteRequest request)
    {
        var note = await _context.VideoNotes
            .FirstOrDefaultAsync(vn => vn.Id == noteId && vn.UserId == userId)
            ?? throw new KeyNotFoundException("Not bulunamadı.");

        note.Text = request.Text;
        note.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"videonotes:{userId}:{note.MediaAssetId}");

        return new VideoNoteDto(note.Id, note.MediaAssetId, note.TimestampSeconds,
            FormatTimestamp(note.TimestampSeconds), note.Text, note.CreatedAt);
    }

    public async Task DeleteNoteAsync(Guid userId, Guid noteId)
    {
        var note = await _context.VideoNotes
            .FirstOrDefaultAsync(vn => vn.Id == noteId && vn.UserId == userId)
            ?? throw new KeyNotFoundException("Not bulunamadı.");

        var mediaAssetId = note.MediaAssetId;
        _context.VideoNotes.Remove(note);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"videonotes:{userId}:{mediaAssetId}");
    }

    private static string FormatTimestamp(int seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}

// ═══════════════════════════════════════════════════════════════
// VIDEO PROGRESS SERVICE
// ═══════════════════════════════════════════════════════════════

public class VideoProgressService : IVideoProgressService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;
    public VideoProgressService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    // Öğrencinin "kaldığı yer" — player açıldığında bu endpoint çağrılır
    public async Task<VideoProgressDto?> GetProgressAsync(Guid userId, Guid mediaAssetId)
    {
        var cacheKey = $"videoprogress:{userId}:{mediaAssetId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var p = await _context.VideoProgresses
                .AsNoTracking()
                .FirstOrDefaultAsync(vp => vp.UserId == userId && vp.MediaAssetId == mediaAssetId);

            return p == null ? null : MapDto(p);
        }, TimeSpan.FromSeconds(30));
    }

    // Heartbeat: Player her 30 saniyede bir bu endpoint'i çağırır
    public async Task<VideoProgressDto?> UpsertProgressAsync(Guid userId, Guid mediaAssetId, UpdateVideoProgressRequest request)
    {
        var progress = await _context.VideoProgresses
            .FirstOrDefaultAsync(vp => vp.UserId == userId && vp.MediaAssetId == mediaAssetId);

        if (progress == null)
        {
            // 🛡️ FK koruması: MediaAsset gerçekten var mı kontrol et
            var assetExists = await _context.MediaAssets.AnyAsync(m => m.Id == mediaAssetId);
            if (!assetExists)
            {
                var cm = await _context.CourseMedias.Include(c => c.Session).FirstOrDefaultAsync(c => c.Id == mediaAssetId);
                if (cm != null)
                {
                    _context.MediaAssets.Add(new MediaAsset
                    {
                        Id = mediaAssetId,
                        CourseId = cm.CourseId,
                        Title = cm.Session?.Title ?? "Ders Kaydı",
                        Status = MURO.Domain.Enums.MediaStatus.Ready
                    });
                }
                else
                {
                    var sr = await _context.SessionRecordings.Include(r => r.Session).FirstOrDefaultAsync(r => r.Id == mediaAssetId);
                    if (sr != null)
                    {
                        _context.MediaAssets.Add(new MediaAsset
                        {
                            Id = mediaAssetId,
                            CourseId = sr.Session.CourseId,
                            Title = sr.Session.Title,
                            Status = MURO.Domain.Enums.MediaStatus.Ready
                        });
                    }
                    else
                    {
                        return null; // Gerçekten hiçbir karşılığı yoksa çık
                    }
                }
            }

            progress = new VideoProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MediaAssetId = mediaAssetId
            };
            _context.VideoProgresses.Add(progress);
        }

        progress.WatchedSeconds += request.WatchedSeconds;
        progress.TotalSeconds = request.TotalSeconds;
        progress.LastPosition = request.LastPosition;
        progress.SkipCount += request.SkipCount;
        progress.ReplayCount += request.ReplayCount;
        progress.UpdatedAt = DateTime.UtcNow;

        // Tamamlandı mı? (%90+ izlenirse veya açık olarak işaretlenirse
        if (request.MarkCompleted || (request.TotalSeconds > 0 && (double)request.WatchedSeconds / request.TotalSeconds >= 0.9))
        {
            progress.CompletedAt ??= DateTime.UtcNow;
        }

        var title = await _context.MediaAssets.Where(m => m.Id == mediaAssetId).Select(m => m.Title).FirstOrDefaultAsync() ?? "Eğitim Videosu";
        progress.AuditDisplayName = title;

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"videoprogress:{userId}:");
        await _cache.RemoveByPrefixAsync($"student:dashboard:{userId}");
        await _cache.RemoveAsync($"analytics:scorecard:{userId}");
        await _cache.RemoveAsync($"analytics:admin_dashboard");
        await _cache.RemoveAsync("analytics:scorecards_list");
        return MapDto(progress);
    }

    // Bir kursa ait videoların öğrenci bazında ilerleme durumu
    public async Task<List<VideoProgressDto>> GetMyCourseProgressAsync(Guid userId, Guid courseId)
    {
        var cacheKey = $"videoprogress:{userId}:course:{courseId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await _context.VideoProgresses
                .AsNoTracking()
                .Where(vp => vp.UserId == userId && vp.MediaAsset.CourseId == courseId)
                .Include(vp => vp.MediaAsset)
                .OrderBy(vp => vp.MediaAsset.CreatedAt)
                .Select(vp => MapDto(vp))
                .ToListAsync();
        }, TimeSpan.FromMinutes(2));
    }

    private static VideoProgressDto MapDto(VideoProgress p)
    {
        var pct = p.TotalSeconds > 0
            ? Math.Round((double)p.WatchedSeconds / p.TotalSeconds * 100, 1)
            : 0;
        return new VideoProgressDto(p.MediaAssetId, p.WatchedSeconds, p.TotalSeconds,
            p.LastPosition, pct, p.SkipCount, p.ReplayCount, p.CompletedAt);
    }
}
