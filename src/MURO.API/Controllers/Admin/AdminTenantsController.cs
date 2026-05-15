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
public class AdminTenantsController : ControllerBase
{
    private readonly IAdminTenantManagementService _managementService;
    private readonly IAdminTenantQuotaService _quotaService;
    private readonly IAdminTenantHealthService _healthService;

    public AdminTenantsController(
        IAdminTenantManagementService managementService,
        IAdminTenantQuotaService quotaService,
        IAdminTenantHealthService healthService)
    {
        _managementService = managementService;
        _quotaService = quotaService;
        _healthService = healthService;
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null)
    {
        var (status_code, data) = await _managementService.GetTenants(page, pageSize, search, status);
        return StatusCode(status_code, data);
    }

    [HttpGet("tenants/{id:guid}")]
    public async Task<IActionResult> GetTenantDetail(Guid id)
    {
        var (status_code, data) = await _managementService.GetTenantDetail(id);
        return StatusCode(status_code, data);
    }

    [HttpPut("tenants/{id:guid}/status")]
    public async Task<IActionResult> UpdateTenantStatus(Guid id, [FromBody] TenantStatusRequest request)
    {
        var (status_code, data) = await _managementService.UpdateTenantStatus(id, request);
        return StatusCode(status_code, data);
    }

    [HttpPut("tenants/{id:guid}/features")]
    public async Task<IActionResult> UpdateTenantFeatures(Guid id, [FromBody] UpdateFeaturesRequest request)
    {
        var (status_code, data) = await _quotaService.UpdateTenantFeatures(id, request);
        return StatusCode(status_code, data);
    }

    [HttpGet("tenants/{id:guid}/quotas")]
    public async Task<IActionResult> GetTenantQuotas(Guid id)
    {
        var (status_code, data) = await _quotaService.GetTenantQuotas(id);
        return StatusCode(status_code, data);
    }

    [HttpPut("tenants/{id:guid}/quotas")]
    public async Task<IActionResult> UpdateTenantQuotas(Guid id, [FromBody] UpdateQuotasRequest request)
    {
        var (status_code, data) = await _quotaService.UpdateTenantQuotas(id, request);
        return StatusCode(status_code, data);
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        var (status_code, data) = await _managementService.CreateTenant(request);
        return StatusCode(status_code, data);
    }

    [HttpGet("tenants/{id:guid}/health")]
    public async Task<IActionResult> GetTenantHealthScore(Guid id)
    {
        var (status_code, data) = await _healthService.GetTenantHealthScore(id);
        return StatusCode(status_code, data);
    }

    [HttpPut("tenants/{id:guid}/maintenance")]
    public async Task<IActionResult> UpdateTenantMaintenance(Guid id, [FromBody] MaintenanceRequest request)
    {
        var (status_code, data) = await _healthService.UpdateTenantMaintenance(id, request);
        return StatusCode(status_code, data);
    }

    [HttpGet("tenants/maintenance")]
    public async Task<IActionResult> GetTenantsMaintenanceStatus()
    {
        var (status_code, data) = await _healthService.GetTenantsMaintenanceStatus();
        return StatusCode(status_code, data);
    }
}
