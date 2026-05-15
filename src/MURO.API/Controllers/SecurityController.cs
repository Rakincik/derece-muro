using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.Interfaces;
using MURO.Application.DTOs.Security;

namespace MURO.API.Controllers;

/// <summary>
/// Güvenlik audit loglarını admin paneline sunar.
/// </summary>
[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/security")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class SecurityController : ControllerBase
{
    private readonly ISecurityService _securityService;
    private readonly ITenantService _tenantService;

    public SecurityController(ISecurityService securityService, ITenantService tenantService)
    {
        _securityService = securityService;
        _tenantService = tenantService;
    }

    private Guid GetTenantId() =>
        _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException("Kurum bilgisi bulunamadı.");

    [HttpGet("events")]
    public async Task<ActionResult<SecurityEventPageDto>> GetEvents(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? userId,
        [FromQuery] string? eventType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var tenantId = GetTenantId();
        var events = await _securityService.GetEventsAsync(tenantId, from, to, userId, eventType, page, pageSize);
        return Ok(events);
    }

    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary()
    {
        var tenantId = GetTenantId();
        var summary = await _securityService.GetSummaryAsync(tenantId);
        return Ok(summary);
    }

    [HttpGet("suspicious")]
    public async Task<ActionResult> GetSuspiciousActivity()
    {
        var tenantId = GetTenantId();
        var suspicious = await _securityService.GetSuspiciousActivityAsync(tenantId);
        return Ok(suspicious);
    }
}
