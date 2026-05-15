namespace MURO.Application.DTOs.Videos;

// ── VideoNote ─────────────────────────────────────────────────────────────
public record VideoNoteDto(
    Guid Id,
    Guid MediaAssetId,
    int TimestampSeconds,
    string TimestampFormatted, // "00:14:30" formatında
    string Text,
    DateTime CreatedAt
);

public record CreateVideoNoteRequest(
    int TimestampSeconds,
    string Text
);

public record UpdateVideoNoteRequest(
    string Text
);

// ── VideoProgress (özet) ───────────────────────────────────────────────────
public record VideoProgressDto(
    Guid MediaAssetId,
    int WatchedSeconds,
    int TotalSeconds,
    int LastPosition,
    double CompletionPercentage,
    int SkipCount,
    int ReplayCount,
    DateTime? CompletedAt
);

public record UpdateVideoProgressRequest(
    int WatchedSeconds,
    int TotalSeconds,
    int LastPosition,
    int SkipCount = 0,
    int ReplayCount = 0,
    bool MarkCompleted = false
);
