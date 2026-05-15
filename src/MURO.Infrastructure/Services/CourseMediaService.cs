using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Media;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class CourseMediaService : ICourseMediaService
{
    private readonly MuroDbContext _context;

    public CourseMediaService(MuroDbContext context)
    {
        _context = context;
    }

    public async Task<List<CourseMediaDto>> GetCourseMediasAsync(Guid tenantId, Guid courseId)
    {
        // 1. Sync live session recordings to CourseMedias automatically
        // We find all SessionRecordings that are NOT in CourseMedias. 
        // If they don't have a MediaAssetId, we create a dummy MediaAsset for them first!
        var unsyncedRecordings = await _context.SessionRecordings
            .Include(r => r.Session)
            .Where(r => r.Session.CourseId == courseId 
                     && r.Session.Course.TenantId == tenantId 
                     && !_context.CourseMedias.Any(cm => cm.CourseId == courseId && cm.MediaAssetId == r.MediaAssetId))
            .ToListAsync();

        if (unsyncedRecordings.Any())
        {
            var maxOrder = await _context.CourseMedias
                .Where(cm => cm.CourseId == courseId)
                .MaxAsync(cm => (int?)cm.OrderIndex) ?? -1;

            foreach (var recording in unsyncedRecordings)
            {
                if (recording.MediaAssetId == null)
                {
                    // Create a dummy MediaAsset for old recordings
                    var asset = new MediaAsset
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        CourseId = courseId,
                        Title = recording.Session.Title + " - Kayıt",
                        Status = MURO.Domain.Enums.MediaStatus.Ready,
                        CreatedAt = recording.CreatedAt
                    };
                    _context.MediaAssets.Add(asset);
                    recording.MediaAssetId = asset.Id;
                    recording.Status = MURO.Domain.Enums.MediaStatus.Ready;
                }

                maxOrder++;
                _context.CourseMedias.Add(new CourseMedia
                {
                    CourseId = courseId,
                    MediaAssetId = recording.MediaAssetId.Value,
                    OrderIndex = maxOrder
                });
            }
            await _context.SaveChangesAsync();
        }

        // 2. Fetch all CourseMedias (which now includes both educational videos and synced live recordings)
        var courseMedias = await _context.CourseMedias
            .Include(cm => cm.MediaAsset)
            .Where(cm => cm.CourseId == courseId && cm.Course.TenantId == tenantId)
            .OrderBy(cm => cm.OrderIndex)
            .Select(cm => new CourseMediaDto(
                cm.Id,
                cm.CourseId,
                cm.MediaAssetId,
                cm.OrderIndex,
                new MediaAssetDto(
                    cm.MediaAsset.Id,
                    cm.MediaAsset.Title,
                    cm.MediaAsset.FilePath,
                    cm.MediaAsset.HlsPath,
                    cm.MediaAsset.ThumbnailPath,
                    cm.MediaAsset.DurationSeconds,
                    cm.MediaAsset.Status.ToString(),
                    cm.CourseId, // Legacy mapping for DTO compatibility
                    cm.Course.Title,
                    cm.MediaAsset.FolderId,
                    cm.MediaAsset.CreatedAt
                )
            ))
            .ToListAsync();

        return courseMedias;
    }

    public async Task<CourseMediaDto> AssignMediaAsync(Guid tenantId, Guid courseId, AssignMediaToCourseRequest request)
    {
        // Verify media belongs to tenant
        var media = await _context.MediaAssets.FirstOrDefaultAsync(m => m.Id == request.MediaAssetId && m.TenantId == tenantId);
        if (media == null) throw new Exception("Media not found");

        // Verify course belongs to tenant
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.TenantId == tenantId);
        if (course == null) throw new Exception("Course not found");

        // Prevent duplicates
        var exists = await _context.CourseMedias.AnyAsync(cm => cm.CourseId == courseId && cm.MediaAssetId == request.MediaAssetId);
        if (exists) throw new Exception("Media already assigned to this course");

        // Get max order index
        var maxOrder = await _context.CourseMedias
            .Where(cm => cm.CourseId == courseId)
            .MaxAsync(cm => (int?)cm.OrderIndex) ?? -1;

        var courseMedia = new CourseMedia
        {
            CourseId = courseId,
            MediaAssetId = request.MediaAssetId,
            OrderIndex = maxOrder + 1
        };

        _context.CourseMedias.Add(courseMedia);
        await _context.SaveChangesAsync();

        return new CourseMediaDto(
            courseMedia.Id,
            courseMedia.CourseId,
            courseMedia.MediaAssetId,
            courseMedia.OrderIndex,
            new MediaAssetDto(
                media.Id, media.Title, media.FilePath, media.HlsPath, media.ThumbnailPath, 
                media.DurationSeconds, media.Status.ToString(), courseId, course.Title, media.FolderId, media.CreatedAt
            )
        );
    }

    public async Task BulkAssignFolderAsync(Guid tenantId, Guid courseId, BulkAssignFolderToCourseRequest request)
    {
        // Verify folder and course
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.TenantId == tenantId);
        if (course == null) throw new Exception("Course not found");

        var folder = await _context.MediaFolders.FirstOrDefaultAsync(f => f.Id == request.FolderId && f.TenantId == tenantId);
        if (folder == null) throw new Exception("Folder not found");

        var assets = await _context.MediaAssets
            .Where(ma => ma.FolderId == request.FolderId && ma.TenantId == tenantId)
            .OrderBy(ma => ma.Title)
            .ToListAsync();

        var maxOrder = await _context.CourseMedias
            .Where(cm => cm.CourseId == courseId)
            .MaxAsync(cm => (int?)cm.OrderIndex) ?? -1;

        foreach (var asset in assets)
        {
            var exists = await _context.CourseMedias.AnyAsync(cm => cm.CourseId == courseId && cm.MediaAssetId == asset.Id);
            if (!exists)
            {
                maxOrder++;
                _context.CourseMedias.Add(new CourseMedia
                {
                    CourseId = courseId,
                    MediaAssetId = asset.Id,
                    OrderIndex = maxOrder
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task RemoveMediaAsync(Guid tenantId, Guid courseId, Guid mediaAssetId)
    {
        var courseMedia = await _context.CourseMedias
            .FirstOrDefaultAsync(cm => cm.CourseId == courseId && cm.MediaAssetId == mediaAssetId && cm.Course.TenantId == tenantId);

        if (courseMedia != null)
        {
            _context.CourseMedias.Remove(courseMedia);
            await _context.SaveChangesAsync();
        }
    }

    public async Task ReorderMediasAsync(Guid tenantId, Guid courseId, ReorderCourseMediaRequest request)
    {
        var courseMedias = await _context.CourseMedias
            .Where(cm => cm.CourseId == courseId && cm.Course.TenantId == tenantId)
            .ToListAsync();

        for (int i = 0; i < request.CourseMediaIds.Count; i++)
        {
            var media = courseMedias.FirstOrDefault(cm => cm.Id == request.CourseMediaIds[i]);
            if (media != null)
            {
                media.OrderIndex = i;
            }
        }

        await _context.SaveChangesAsync();
    }
}
