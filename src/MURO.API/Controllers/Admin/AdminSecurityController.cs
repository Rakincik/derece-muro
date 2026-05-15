using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MURO.Application.DTOs.Admin;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers.Admin;

[EnableRateLimiting("ApiPolicy")]
[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/v1/admin")]
public class AdminSecurityController : ControllerBase
{
    private readonly IAdminSecurityService _securityService;
    private readonly IAdminUserService _userService;

    public AdminSecurityController(IAdminSecurityService securityService, IAdminUserService userService)
    {
        _securityService = securityService;
        _userService = userService;
    }

    [HttpGet("security/users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] string? search = null, [FromQuery] string? role = null, [FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var (status_code, data) = await _userService.GetAllUsers(search, role, status, page, pageSize);
        return StatusCode(status_code, data);
    }

    [HttpGet("security/roles")]
    public async Task<IActionResult> GetRoleDistribution()
    {
        var (status_code, data) = await _userService.GetRoleDistribution();
        return StatusCode(status_code, data);
    }

    [HttpGet("security/events")]
    public async Task<IActionResult> GetSecurityEvents([FromQuery] string? eventType = null, [FromQuery] Guid? tenantId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var (status_code, data) = await _securityService.GetSecurityEvents(eventType, tenantId, page, pageSize);
        return StatusCode(status_code, data);
    }

    [HttpGet("security/summary")]
    public async Task<IActionResult> GetSecuritySummary()
    {
        var (status_code, data) = await _securityService.GetSecuritySummary();
        return StatusCode(status_code, data);
    }

    [HttpGet("security/locked-accounts")]
    public async Task<IActionResult> GetLockedAccounts()
    {
        var (status_code, data) = await _securityService.GetLockedAccounts();
        return StatusCode(status_code, data);
    }

    [HttpPost("security/unlock-account/{userId:guid}")]
    public async Task<IActionResult> UnlockAccount(Guid userId)
    {
        var (status_code, data) = await _securityService.UnlockAccount(userId);
        return StatusCode(status_code, data);
    }
}

