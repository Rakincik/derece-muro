using MURO.Application.DTOs;
using MURO.Application.DTOs.Users;

namespace MURO.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserListDto>> GetUsersAsync(Guid tenantId, int page, int pageSize, string? search, string? role, string? sortBy, string? sortDir);
    Task<UserDetailDto> GetUserByIdAsync(Guid tenantId, Guid userId);
    Task<UserListDto> CreateUserAsync(Guid tenantId, CreateUserRequest request);
    Task<List<UserListDto>> BulkCreateUsersAsync(Guid tenantId, List<CreateUserRequest> users);
    Task<UserListDto> UpdateUserAsync(Guid tenantId, Guid userId, UpdateUserRequest request);
    Task DeleteUserAsync(Guid tenantId, Guid userId);
    Task BulkDeleteUsersAsync(Guid tenantId, List<Guid> userIds);
    Task AssignToGroupAsync(Guid tenantId, Guid userId, Guid groupId);
    Task AssignToCourseAsync(Guid tenantId, Guid userId, Guid courseId, string mode);
    Task<byte[]> ExportUsersAsync(Guid tenantId, string? role);
}
