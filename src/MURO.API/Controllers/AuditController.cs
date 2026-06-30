using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/audit")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    
    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
            }

    

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var result = await _auditService.GetLogsAsync(page, pageSize, action, entityType, search, from, to);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AuditSummaryDto>> GetSummary(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow;
        return Ok(await _auditService.GetSummaryAsync(f, t));
    }

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<UserAuditSummaryDto>>> GetUserAudits(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null)
    {
        var result = await _auditService.GetUserAuditSummariesAsync(page, pageSize, search, sortBy);
        return Ok(result);
    }

    [HttpGet("suspicious")]
    public async Task<ActionResult<List<SuspiciousUserDto>>> GetSuspiciousUsers()
    {
        var result = await _auditService.GetSuspiciousUsersAsync();
        return Ok(result);
    }
}
