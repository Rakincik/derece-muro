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
public class AdminAnalyticsController : ControllerBase
{
    private readonly IAdminAnalyticsService _analyticsService;

    public AdminAnalyticsController(IAdminAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("analytics/sessions/history")]
    public async Task<IActionResult> GetSessionHistory([FromQuery] int days = 7)
    {
        var (status_code, data) = await _analyticsService.GetSessionHistory(days);
        return StatusCode(status_code, data);
    }

    [HttpGet("analytics/sessions/heatmap")]
    public async Task<IActionResult> GetSessionHeatmap([FromQuery] int weeks = 4)
    {
        var (status_code, data) = await _analyticsService.GetSessionHeatmap(weeks);
        return StatusCode(status_code, data);
    }
}
