using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Groups;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/groups")]
[Authorize(Roles = "Admin,SuperAdmin,Assistant")]
public class GroupsController : ControllerBase
{
    private readonly IGroupService _groupService;
    private readonly ITenantService _tenantService;
    private readonly IBackgroundJobQueue _jobQueue;

    public GroupsController(IGroupService groupService, ITenantService tenantService, IBackgroundJobQueue jobQueue)
    {
        _groupService = groupService;
        _tenantService = tenantService;
        _jobQueue = jobQueue;
    }

    private Guid GetTenantId() =>
        _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException("Kurum bilgisi bulunamadı.");
    private Guid? GetActorId() => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<ActionResult<PagedResult<GroupListDto>>> GetGroups(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        return Ok(await _groupService.GetGroupsAsync(GetTenantId(), page, pageSize, search));
    }

    [HttpGet("tree")]
    public async Task<ActionResult<List<GroupTreeDto>>> GetGroupTree()
    {
        return Ok(await _groupService.GetGroupTreeAsync(GetTenantId()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GroupDetailDto>> GetGroup(Guid id)
    {
        return Ok(await _groupService.GetGroupByIdAsync(GetTenantId(), id));
    }

    [HttpPost]
    public async Task<ActionResult<GroupListDto>> CreateGroup([FromBody] CreateGroupRequest request)
    {
        var group = await _groupService.CreateGroupAsync(GetTenantId(), request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetActorId(), null, "Create", "Group", group.Id.ToString(), request.Name, null, GetIp()));
        return Created($"/api/v1/groups/{group.Id}", group);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GroupListDto>> UpdateGroup(Guid id, [FromBody] UpdateGroupRequest request)
    {
        var result = await _groupService.UpdateGroupAsync(GetTenantId(), id, request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetActorId(), null, "Update", "Group", id.ToString(), request.Name, null, GetIp()));
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid id)
    {
        await _groupService.DeleteGroupAsync(GetTenantId(), id);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetActorId(), null, "Delete", "Group", id.ToString(), null, null, GetIp()));
        return NoContent();
    }

    [HttpDelete("{id:guid}/force")]
    public async Task<IActionResult> ForceDeleteGroup(Guid id)
    {
        await _groupService.ForceDeleteGroupAsync(GetTenantId(), id);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetActorId(), null, "ForceDelete", "Group", id.ToString(), null, null, GetIp()));
        return NoContent();
    }

    [HttpPost("{id:guid}/clone")]
    public async Task<ActionResult<GroupListDto>> CloneGroup(Guid id, [FromQuery] string newName, [FromQuery] bool copyMembers = false, [FromQuery] bool copyCourses = false)
    {
        var group = await _groupService.CloneGroupAsync(GetTenantId(), id, newName, copyMembers, copyCourses);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetActorId(), null, "Clone", "Group", group.Id.ToString(), newName, null, GetIp()));
        return Ok(group);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMembers(Guid id, [FromBody] AddGroupMembersRequest request)
    {
        await _groupService.AddMembersAsync(GetTenantId(), id, request.UserIds);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetActorId(), null, "AddMember", "Group", id.ToString(), null, $"{request.UserIds.Count} üye eklendi", GetIp()));
        return Ok(new { message = "Üyeler eklendi." });
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        await _groupService.RemoveMemberAsync(GetTenantId(), id, userId);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetActorId(), null, "RemoveMember", "Group", id.ToString(), null, $"UserId: {userId}", GetIp()));
        return NoContent();
    }

    [HttpPost("{id:guid}/members/move")]
    public async Task<IActionResult> MoveMembers(Guid id, [FromBody] MoveMembersRequest request)
    {
        await _groupService.MoveMembersAsync(GetTenantId(), id, request.ToGroupId, request.UserIds);
        return Ok(new { message = "Üyeler taşındı." });
    }
}
