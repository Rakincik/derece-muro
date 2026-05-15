using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/device-sessions")]
[Authorize]
public class DeviceSessionsController : ControllerBase
{
    private readonly IAuthSessionService _authService;

    public DeviceSessionsController(IAuthSessionService authService)
    {
        _authService = authService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMySessions()
    {
        var userId = GetUserId();
        var sessions = await _authService.GetActiveSessionsAsync(userId);
        return Ok(sessions);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id)
    {
        var userId = GetUserId();
        await _authService.RevokeSessionAsync(userId, id);
        return NoContent();
    }

    [HttpDelete("revoke-all")]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var userId = GetUserId();
        await _authService.LogoutAsync(userId);
        return NoContent();
    }

    public record UpdatePushTokenRequest(string Token);

    [HttpPut("push-token")]
    public async Task<IActionResult> UpdatePushToken([FromBody] UpdatePushTokenRequest request)
    {
        var userId = GetUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();
        
        await _authService.UpdatePushTokenAsync(userId, request.Token, ipAddress, userAgent);
        
        return NoContent();
    }
}
