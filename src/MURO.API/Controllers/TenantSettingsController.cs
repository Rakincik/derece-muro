using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MURO.Application.Interfaces;
using MURO.Application.DTOs.Tenants;

namespace MURO.API.Controllers;

[EnableRateLimiting("ApiPolicy")]
[ApiController]
[Route("api/v1/tenant/settings")]
[Authorize]
public class TenantSettingsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantSettingsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _tenantService.GetSettingsAsync();
        if (settings == null) return NotFound(new { message = "Ayarlar bulunamadı veya tenant yetkisi yok." });

        return Ok(settings);
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateTenantSettingsRequest req)
    {
        var updated = await _tenantService.UpdateSettingsAsync(
            req.Name, 
            req.LogoUrl, 
            req.FaviconUrl, 
            req.PrimaryColor, 
            req.AccentColor, 
            req.FooterText
        );

        if (updated == null) return NotFound(new { message = "Tenant bulunamadı veya yetki yok." });

        return Ok(new { message = "Ayarlar güncellendi.", data = updated });
    }
}
