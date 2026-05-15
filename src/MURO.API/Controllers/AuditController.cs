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
    private readonly ITenantService _tenantService;

    public AuditController(IAuditService auditService, ITenantService tenantService)
    {
        _auditService = auditService;
        _tenantService = tenantService;
    }

    private Guid GetTenantId() =>
        _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException("Kurum bilgisi bulunamadı.");

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
        var result = await _auditService.GetLogsAsync(GetTenantId(), page, pageSize, action, entityType, search, from, to);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AuditSummaryDto>> GetSummary(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow;
        return Ok(await _auditService.GetSummaryAsync(GetTenantId(), f, t));
    }
}
