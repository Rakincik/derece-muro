using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/recordings")]
[Authorize]
public class RecordingsController : ControllerBase
{
    private readonly IRecordingService _recordingService;
    private readonly ITenantService _tenantService;

    public RecordingsController(IRecordingService recordingService, ITenantService tenantService)
    {
        _recordingService = recordingService;
        _tenantService = tenantService;
    }

    private Guid TenantId =>
        _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException("Kurum bilgisi bulunamadı.");

    // ── GET /api/v1/recordings ────────────────────────────────────────────────
    /// <summary>
    /// Bu tenant'a ait tüm canlı ders kayıtlarını listele.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecordings()
    {
        var tenantId = TenantId;
        var userId = Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value, out var uid) ? uid : Guid.Empty;
        var role = User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        var recordings = await _recordingService.GetRecordingsAsync(tenantId, userId, role);

        return Ok(recordings);
    }

    // ── DELETE /api/v1/recordings/{id} ────────────────────────────────────────
    /// <summary>
    /// Kaydı sil. İlişkili MediaAsset de silinir.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRecording(Guid id)
    {
        var tenantId = TenantId;

        try
        {
            await _recordingService.DeleteRecordingAsync(tenantId, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
