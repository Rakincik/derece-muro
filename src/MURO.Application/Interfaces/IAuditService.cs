using MURO.Application.DTOs;

namespace MURO.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(Guid? userId, string? userName, string action,
                  string entityType, string? entityId, string? entityName,
                  string? details = null, string? ipAddress = null);

    Task<PagedResult<AuditLogDto>> GetLogsAsync(int page, int pageSize,
                                                 string? action = null, string? entityType = null,
                                                 string? search = null, DateTime? from = null, DateTime? to = null);

    Task<AuditSummaryDto> GetSummaryAsync(DateTime from, DateTime to);
    
    Task<PagedResult<UserAuditSummaryDto>> GetUserAuditSummariesAsync(int page, int pageSize, string? search = null, string? sortBy = null);
    
    Task<List<SuspiciousUserDto>> GetSuspiciousUsersAsync();
}

public record AuditLogDto(
    Guid Id, Guid? UserId, string? UserName,
    string Action, string EntityType, string? EntityId, string? EntityName,
    string? Details, string? IpAddress, DateTime CreatedAt
);

public record AuditSummaryDto(
    int TotalCount,
    int CreateCount,
    int UpdateCount,
    int DeleteCount,
    int NightActivityCount,
    Dictionary<string, int> TopEntities
);

public record UserAuditSummaryDto(
    Guid? UserId,
    string? UserName,
    string? Email,
    string? AvatarUrl,
    int ActionCount,
    DateTime LastActionAt,
    string? LastAction
);

public record SuspiciousUserDto(
    Guid? UserId,
    string? UserName,
    string AlertType,
    int EventCount,
    DateTime LastEventAt
);
