using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Videos;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class RecordingService : IRecordingService
{
    private readonly MuroDbContext _context;
    private readonly IGroupAccessService _groupAccess;

    public RecordingService(MuroDbContext context, IGroupAccessService groupAccess)
    {
        _context = context;
        _groupAccess = groupAccess;
    }

    public async Task<List<SessionRecordingDto>> GetRecordingsAsync(Guid tenantId, Guid userId, string? role)
    {
        var query = _context.SessionRecordings
            .AsNoTracking()
            .Include(r => r.Session)
                .ThenInclude(s => s.Course)
            .Include(r => r.MediaAsset)
            .Where(r => r.Session.Course.TenantId == tenantId && r.Session.Description != "Video (VOD)");

        // Student ise sadece erişebildiği kursların kayıtlarını görsün
        if (role == "Student")
        {
            var accessibleCourseIds = await _groupAccess.GetAccessibleCourseIdsAsync(tenantId, userId);
            query = query.Where(r => accessibleCourseIds.Contains(r.Session.CourseId));
        }

        var recordings = await query
            .OrderByDescending(r => r.Session.ScheduledStart ?? r.CreatedAt)
            .Select(r => new SessionRecordingDto(
                r.Id,
                r.SessionId,
                r.Session.Title,
                r.Session.Course.Title,
                r.Session.ScheduledStart,
                r.Status.ToString(),
                r.MediaAsset != null ? r.MediaAsset.FilePath : null,
                r.MediaAsset != null ? r.MediaAsset.HlsPath : null,
                r.MediaAsset != null ? r.MediaAsset.ThumbnailPath : null,
                r.MediaAsset != null ? (int?)r.MediaAsset.DurationSeconds : null,
                r.CreatedAt,
                r.MediaAssetId
            ))
            .ToListAsync();

        return recordings;
    }

    public async Task DeleteRecordingAsync(Guid tenantId, Guid id)
    {
        var recording = await _context.SessionRecordings
            .Include(r => r.Session).ThenInclude(s => s.Course)
            .Include(r => r.MediaAsset)
            .FirstOrDefaultAsync(r => r.Id == id && r.Session.Course.TenantId == tenantId);

        if (recording == null) throw new KeyNotFoundException("Kayıt bulunamadı.");

        // MediaAsset varsa onu da kaldır
        if (recording.MediaAsset != null)
            _context.MediaAssets.Remove(recording.MediaAsset);

        _context.SessionRecordings.Remove(recording);
        await _context.SaveChangesAsync();
    }
}
