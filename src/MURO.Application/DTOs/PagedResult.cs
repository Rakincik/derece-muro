namespace MURO.Application.DTOs;

/// <summary>
/// Standard paginated response wrapper.
/// </summary>
public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
