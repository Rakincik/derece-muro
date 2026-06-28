using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;
using MURO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MURO.API.Controllers;

/// <summary>
/// Sistem sağlık, metrik ve VEP entegrasyon endpoint'leri.
/// </summary>
[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/system")]
public class SystemController : ControllerBase
{
    private readonly ISystemHealthService _health;
    private readonly IVepWebhookService _webhook;
    private readonly MuroDbContext _db;
    private readonly ISecurityService _security;

    public SystemController(ISystemHealthService health, IVepWebhookService webhook, MuroDbContext db, ISecurityService security)
    {
        _health = health;
        _webhook = webhook;
        _db = db;
        _security = security;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 1. DETAYLI SAĞLIK KONTROLÜ
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDetailedHealth()
    {
        var report = await _health.GetHealthReportAsync();
        var statusCode = report.OverallStatus switch
        {
            "healthy" => 200,
            "degraded" => 200,
            _ => 503
        };
        return StatusCode(statusCode, report);
    }

    [HttpGet("/api/v1/tenant/branding")]
    [AllowAnonymous]
    public IActionResult GetTenantBranding()
    {
        // Frontend'in çökmemesi için sahte bir branding dönüyoruz (Single-tenant).
        return Ok(new
        {
            ThemeColor = "#1D4ED8",
            LogoUrl = "/icon.png",
            FaviconUrl = "/favicon.ico",
            CustomCss = "",
            Title = "Monopol"
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. VEP WEBHOOK DURUMU
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("vep/status")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetVepStatus()
    {
        var configured = await _webhook.IsConfiguredAsync();
        return Ok(new
        {
            configured,
            message = configured ? "VEP webhook aktif." : "VEP webhook yapılandırılmamış."
        });
    }

    [HttpPost("vep/test")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> TestVepWebhook()
    {
        var configured = await _webhook.IsConfiguredAsync();
        if (!configured)
            return BadRequest(new { error = "VEP webhook URL yapılandırılmamış." });

        await _webhook.NotifyAsync(new VepWebhookEvent
        {
            EventType = "system.test",
            TenantCode = "system",
            Data = new Dictionary<string, object>
            {
                ["message"] = "Test başarılı!",
                ["sentAt"] = DateTime.UtcNow.ToString("o"),
            }
        });
        return Ok(new { success = true, message = "Test webhook gönderildi." });
    }
}
