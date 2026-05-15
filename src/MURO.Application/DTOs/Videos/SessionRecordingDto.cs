namespace MURO.Application.DTOs.Videos;

public record SessionRecordingDto(
    Guid Id,
    Guid SessionId,
    string SessionTitle,
    string CourseTitle,
    DateTime? ScheduledStart,
    string Status,
    string? PlaybackUrl,
    string? HlsPath,
    string? ThumbnailPath,
    int? DurationSeconds,
    DateTime CreatedAt,
    Guid? MediaAssetId = null
);
