using MURO.Domain.Enums;

namespace MURO.Application.DTOs.Podcasts;

public record PodcastDto(
    Guid Id, Guid? CourseId, string? CourseTitle,
    string Title, string? TextContent, string? GeneratedScript, string? AudioFilePath,
    int? DurationSeconds, MediaStatus Status, DateTime CreatedAt
);

public record CreatePodcastRequest(
    Guid? CourseId, string Title, string? TextContent, string? AudioFilePath
);

public record UpdatePodcastRequest(
    string? Title, string? TextContent, string? AudioFilePath, string? Status
);

/// <summary>
/// Admin'in AI üretimi için gönderdiği istek.
/// </summary>
public record GeneratePodcastRequest(
    string Title,
    Guid? CourseId,
    string RawText,
    string Voice = "tr-TR-AhmetNeural"
);
