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
public class AdminSessionsController : ControllerBase
{
    private readonly IAdminSessionService _sessionService;

    public AdminSessionsController(IAdminSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet("sessions/active")]
    public async Task<IActionResult> GetActiveSessions()
    {
        var (status_code, data) = await _sessionService.GetActiveSessions();
        return StatusCode(status_code, data);
    }

    [HttpGet("sessions/today")]
    public async Task<IActionResult> GetTodaySessions()
    {
        var (status_code, data) = await _sessionService.GetTodaySessions();
        return StatusCode(status_code, data);
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<IActionResult> GetSessionDetail(Guid id)
    {
        var (status_code, data) = await _sessionService.GetSessionDetail(id);
        return StatusCode(status_code, data);
    }

    [HttpPost("sessions/{id:guid}/force-end")]
    public async Task<IActionResult> ForceEndSession(Guid id)
    {
        var (status_code, data) = await _sessionService.ForceEndSession(id);
        return StatusCode(status_code, data);
    }

    [HttpGet("recordings")]
    public async Task<IActionResult> GetRecordings([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, [FromQuery] Guid? tenantId = null)
    {
        var (status_code, data) = await _sessionService.GetRecordings(page, pageSize, status, tenantId);
        return StatusCode(status_code, data);
    }
}
