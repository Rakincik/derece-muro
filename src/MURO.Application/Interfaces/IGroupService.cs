using MURO.Application.DTOs;
using MURO.Application.DTOs.Groups;

namespace MURO.Application.Interfaces;

public interface IGroupService
{
    Task<PagedResult<GroupListDto>> GetGroupsAsync(Guid tenantId, int page, int pageSize, string? search);
    Task<List<GroupTreeDto>> GetGroupTreeAsync(Guid tenantId);
    Task<GroupDetailDto> GetGroupByIdAsync(Guid tenantId, Guid groupId);
    Task<GroupListDto> CreateGroupAsync(Guid tenantId, CreateGroupRequest request);
    Task<GroupListDto> UpdateGroupAsync(Guid tenantId, Guid groupId, UpdateGroupRequest request);
    Task DeleteGroupAsync(Guid tenantId, Guid groupId);
    Task ForceDeleteGroupAsync(Guid tenantId, Guid groupId);
    Task<GroupListDto> CloneGroupAsync(Guid tenantId, Guid groupId, string newName, bool copyMembers, bool copyCourses);
    Task AddMembersAsync(Guid tenantId, Guid groupId, List<Guid> userIds);
    Task RemoveMemberAsync(Guid tenantId, Guid groupId, Guid userId);
    Task MoveMembersAsync(Guid tenantId, Guid fromGroupId, Guid toGroupId, List<Guid> userIds);
}
