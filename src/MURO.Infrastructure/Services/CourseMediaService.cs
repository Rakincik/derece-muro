using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Media;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class CourseMediaService : ICourseMediaService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public CourseMediaService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<CourseMediaDto>> GetCourseMediasAsync(Guid courseId)
    {
        // CourseMedias only fetch actual assigned videos from the media library.
        // Live session recordings (SessionRecordings) remain purely within the Session hierarchy
        // and are NOT auto-synced into CourseMedia.

        // 2. Fetch all CourseMedias (which now includes both educational videos, and exams)
        var courseMedias = await _context.CourseMedias
            .Include(cm => cm.MediaAsset)
            .Include(cm => cm.Exam)
            .Include(cm => cm.Session)
            .Where(cm => cm.CourseId == courseId )
            .OrderBy(cm => cm.OrderIndex)
            .Select(cm => new CourseMediaDto(
                cm.Id,
                cm.CourseId,
                cm.MediaAssetId,
                cm.OrderIndex,
                cm.MediaAsset != null ? new MediaAssetDto(
                    cm.MediaAsset.Id,
                    cm.MediaAsset.Title,
                    cm.MediaAsset.FilePath,
                    cm.MediaAsset.HlsPath,
                    cm.MediaAsset.ThumbnailPath,
                    cm.MediaAsset.DurationSeconds,
                    cm.MediaAsset.Status.ToString(),
                    cm.CourseId,
                    cm.Course.Title,
                    cm.MediaAsset.FolderId,
                    cm.MediaAsset.CreatedAt,
                    cm.MediaAsset.Tags
                ) : null,
                cm.ExamId,
                cm.Exam != null ? cm.Exam.Title : null,
                cm.SessionId,
                cm.Session != null ? cm.Session.Title : null,
                cm.SessionId != null ? "Session" : (cm.ExamId != null ? "Exam" : "Media")
            ))
            .ToListAsync();

        if (!courseMedias.Any(cm => cm.OrderIndex >= 0))
        {
            var comparer = new NaturalStringComparer();
            courseMedias = courseMedias.OrderBy(cm => {
                if (cm.Type == "Exam") return cm.ExamTitle ?? "";
                if (cm.Type == "Session") return cm.SessionTitle ?? "";
                return cm.MediaAsset?.Title ?? "";
            }, comparer).ToList();
        }

        return courseMedias;
    }

    public async Task<CourseMediaDto> AssignMediaAsync(Guid courseId, AssignMediaToCourseRequest request)
    {
        // Verify media belongs to tenant
        var media = await _context.MediaAssets.FirstOrDefaultAsync(m => m.Id == request.MediaAssetId );
        if (media == null) throw new Exception("Media not found");

        // Verify course belongs to tenant
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId );
        if (course == null) throw new Exception("Course not found");

        // Prevent duplicates
        var exists = await _context.CourseMedias.AnyAsync(cm => cm.CourseId == courseId && cm.MediaAssetId == request.MediaAssetId);
        if (exists) throw new Exception("Media already assigned to this course");

        var hasManualSort = await _context.CourseMedias
            .AnyAsync(cm => cm.CourseId == courseId && cm.OrderIndex >= 0);

        int orderIndex = -1;
        if (hasManualSort)
        {
            var maxOrder = await _context.CourseMedias
                .Where(cm => cm.CourseId == courseId)
                .MaxAsync(cm => (int?)cm.OrderIndex) ?? -1;
            orderIndex = maxOrder + 1;
        }

        var courseMedia = new CourseMedia
        {
            CourseId = courseId,
            MediaAssetId = request.MediaAssetId,
            OrderIndex = orderIndex
        };

        _context.CourseMedias.Add(courseMedia);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");

        return new CourseMediaDto(
            courseMedia.Id,
            courseMedia.CourseId,
            courseMedia.MediaAssetId,
            courseMedia.OrderIndex,
            new MediaAssetDto(
                media.Id, media.Title, media.FilePath, media.HlsPath, media.ThumbnailPath, 
                media.DurationSeconds, media.Status.ToString(), courseId, course.Title, media.FolderId, media.CreatedAt, media.Tags
            ),
            null, null, null, null, "Media"
        );
    }

    public async Task<CourseMediaDto> AssignExamAsync(Guid courseId, AssignExamToCourseRequest request)
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId );
        if (exam == null) throw new Exception("Exam not found");

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId );
        if (course == null) throw new Exception("Course not found");

        var exists = await _context.CourseMedias.AnyAsync(cm => cm.CourseId == courseId && cm.ExamId == request.ExamId);
        if (exists) throw new Exception("Exam already assigned to this course");

        var hasManualSort = await _context.CourseMedias
            .AnyAsync(cm => cm.CourseId == courseId && cm.OrderIndex >= 0);

        int orderIndex = -1;
        if (hasManualSort)
        {
            var maxOrder = await _context.CourseMedias
                .Where(cm => cm.CourseId == courseId)
                .MaxAsync(cm => (int?)cm.OrderIndex) ?? -1;
            orderIndex = maxOrder + 1;
        }

        var courseMedia = new CourseMedia
        {
            CourseId = courseId,
            ExamId = request.ExamId,
            OrderIndex = orderIndex
        };

        _context.CourseMedias.Add(courseMedia);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");

        return new CourseMediaDto(
            courseMedia.Id,
            courseMedia.CourseId,
            null,
            courseMedia.OrderIndex,
            null,
            exam.Id,
            exam.Title,
            null,
            null,
            "Exam"
        );
    }

    public async Task BulkAssignFolderAsync(Guid courseId, BulkAssignFolderToCourseRequest request)
    {
        // Verify folder and course
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId );
        if (course == null) throw new Exception("Course not found");

        var folder = await _context.MediaFolders.FirstOrDefaultAsync(f => f.Id == request.FolderId );
        if (folder == null) throw new Exception("Folder not found");

        var assets = await _context.MediaAssets
            .Where(ma => ma.FolderId == request.FolderId )
            .OrderBy(ma => ma.Title)
            .ToListAsync();

        var hasManualSort = await _context.CourseMedias
            .AnyAsync(cm => cm.CourseId == courseId && cm.OrderIndex >= 0);

        int orderIndex = -1;
        if (hasManualSort)
        {
            var maxOrder = await _context.CourseMedias
                .Where(cm => cm.CourseId == courseId)
                .MaxAsync(cm => (int?)cm.OrderIndex) ?? -1;
            orderIndex = maxOrder + 1;
        }

        foreach (var asset in assets)
        {
            var exists = await _context.CourseMedias.AnyAsync(cm => cm.CourseId == courseId && cm.MediaAssetId == asset.Id);
            if (!exists)
            {
                _context.CourseMedias.Add(new CourseMedia
                {
                    CourseId = courseId,
                    MediaAssetId = asset.Id,
                    OrderIndex = orderIndex
                });
                if (hasManualSort)
                {
                    orderIndex++;
                }
            }
        }

        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"courses:");
    }

    public async Task RemoveMediaAsync(Guid courseId, Guid mediaAssetId)
    {
        var courseMedia = await _context.CourseMedias
            .FirstOrDefaultAsync(cm => cm.CourseId == courseId && cm.MediaAssetId == mediaAssetId );

        if (courseMedia != null)
        {
            _context.CourseMedias.Remove(courseMedia);
            await _context.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync($"courses:");
        }
    }

    public async Task RemoveItemAsync(Guid courseId, Guid courseMediaId)
    {
        var courseMedia = await _context.CourseMedias
            .FirstOrDefaultAsync(cm => cm.CourseId == courseId && cm.Id == courseMediaId );

        if (courseMedia != null)
        {
            _context.CourseMedias.Remove(courseMedia);
            await _context.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync($"courses:");
        }
    }

    public async Task ReorderMediasAsync(Guid courseId, ReorderCourseMediaRequest request)
    {
        var courseMedias = await _context.CourseMedias
            .Where(cm => cm.CourseId == courseId )
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
        await _cache.RemoveByPrefixAsync($"courses:");
    }

    private class NaturalStringComparer : IComparer<string>
    {
        private static readonly CultureInfo TurkishCulture = new CultureInfo("tr-TR");

        public int Compare(string? x, string? y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int ix = 0;
            int iy = 0;
            while (ix < x.Length && iy < y.Length)
            {
                if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
                {
                    string numX = "";
                    while (ix < x.Length && char.IsDigit(x[ix])) numX += x[ix++];
                    string numY = "";
                    while (iy < y.Length && char.IsDigit(y[iy])) numY += y[iy++];

                    if (long.TryParse(numX, out long valX) && long.TryParse(numY, out long valY))
                    {
                        int comp = valX.CompareTo(valY);
                        if (comp != 0) return comp;
                    }
                    else
                    {
                        int comp = string.Compare(numX, numY, true, TurkishCulture);
                        if (comp != 0) return comp;
                    }
                }
                else
                {
                    char cx = x[ix];
                    char cy = y[iy];
                    
                    int comp = string.Compare(cx.ToString(), cy.ToString(), true, TurkishCulture);
                    if (comp != 0) return comp;
                    ix++;
                    iy++;
                }
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
