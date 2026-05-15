using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting("GlobalPolicy")]
[ApiController]
[Route("health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly ISystemHealthService _systemHealthService;
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public HealthController(ISystemHealthService systemHealthService)
    {
        _systemHealthService = systemHealthService;
    }

    [HttpGet("live")]
    public IActionResult Liveness()
    {
        return Ok(new
        {
            status = "Alive",
            uptime = (DateTime.UtcNow - StartTime).ToString(@"d\.hh\:mm\:ss")
        });
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Readiness()
    {
        var isReady = await _systemHealthService.CheckReadinessAsync();
        if (isReady)
        {
            return Ok(new { status = "Ready" });
        }
        
        return StatusCode(503, new { status = "Unavailable", reason = "Core services down" });
    }
}
