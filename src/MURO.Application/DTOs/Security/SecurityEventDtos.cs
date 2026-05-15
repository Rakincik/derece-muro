namespace MURO.Application.DTOs.Security;

public record SecurityEventDto(
    Guid Id,
    Guid? UserId,
    string? UserFullName,
    string EventType,
    string? IpAddress,
    string? UserAgent,
    string? Details,
    DateTime CreatedAt
);

public record SecurityEventPageDto(
    List<SecurityEventDto> Items,
    int Total,
    int Page,
    int PageSize
);

public record SecuritySummaryDto(
    string EventType,
    int Count
);
