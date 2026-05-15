using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[Authorize]
[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/packages")]
[Authorize(Roles = "Admin,SuperAdmin,Assistant")]
public class PackagesController : ControllerBase
{
    private readonly IPackageService _packages;
    private readonly ITenantService _tenantService;
    private readonly IBackgroundJobQueue _jobQueue;

    public PackagesController(IPackageService packages, ITenantService tenantService, IBackgroundJobQueue jobQueue)
    {
        _packages = packages;
        _tenantService = tenantService;
        _jobQueue = jobQueue;
    }

    private Guid GetTenantId() => _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException();
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ── Paket Listesi ─────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<IActionResult> GetPackages()
        => Ok(await _packages.GetPackagesAsync(GetTenantId()));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<IActionResult> GetPackage(Guid id)
    {
        try { return Ok(await _packages.GetPackageByIdAsync(GetTenantId(), id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── Paket CRUD ────────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePackage([FromBody] CreatePackageRequest request)
    {
        var result = await _packages.CreatePackageAsync(GetTenantId(), request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Create", "Package", result.Id.ToString(), request.Name, null, GetIp()));
        return CreatedAtAction(nameof(GetPackage), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] UpdatePackageRequest request)
    {
        try { return Ok(await _packages.UpdatePackageAsync(GetTenantId(), id, request)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePackage(Guid id)
    {
        try
        {
            await _packages.DeletePackageAsync(GetTenantId(), id);
            await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Delete", "Package", id.ToString(), null, null, GetIp()));
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── Kullanıcı Paket İşlemleri ─────────────────────────────────────────────

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ActivateForUser(Guid id, [FromBody] ActivatePackageRequest request)
    {
        try
        {
            var result = await _packages.ActivateUserPackageAsync(
                request.UserId, id, request.OrderId, "admin_manual",
                request.ManualExpiresAt);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("user/{userId:guid}")]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<IActionResult> GetUserPackages(Guid userId)
        => Ok(await _packages.GetUserPackagesAsync(userId));

    [HttpPost("user-packages/{userPackageId:guid}/extend")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExtendPackage(Guid userPackageId, [FromBody] ExtendPackageRequest request)
    {
        try { return Ok(await _packages.ExtendPackageAsync(userPackageId, request.ExtraDays)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("user-packages/{userPackageId:guid}/cancel")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CancelPackage(Guid userPackageId)
    {
        try { await _packages.CancelPackageAsync(userPackageId); return Ok(new { success = true }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── Webhook Bilgisi ───────────────────────────────────────────────────────

    [HttpGet("webhook-info")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetWebhookInfo([FromServices] IConfiguration config)
    {
        var secret = config["Webhook:Secret"];
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(new
        {
            purchaseUrl = $"{baseUrl}/api/v1/webhooks/purchase",
            cancelUrl   = $"{baseUrl}/api/v1/webhooks/cancel",
            secretHint  = secret?.Length > 8 ? $"{secret[..4]}...{secret[^4..]}" : "***",
            signatureHeader = "X-Webhook-Signature",
            signatureAlgo   = "HMAC-SHA256 hex lowercase"
        });
    }
}

public record ActivatePackageRequest(Guid UserId, string? OrderId, DateTime? ManualExpiresAt);
public record ExtendPackageRequest(int ExtraDays);
