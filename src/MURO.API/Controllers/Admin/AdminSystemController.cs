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
public class AdminSystemController : ControllerBase
{
    private readonly IAdminHealthService _healthService;
    private readonly IAdminBackupService _backupService;
    private readonly IAdminConfigService _configService;

    public AdminSystemController(
        IAdminHealthService healthService, 
        IAdminBackupService backupService, 
        IAdminConfigService configService)
    {
        _healthService = healthService;
        _backupService = backupService;
        _configService = configService;
    }

    [HttpGet("bbb-health")]
    public async Task<IActionResult> GetBbbHealth()
    {
        var (status_code, data) = await _healthService.GetBbbHealth();
        return StatusCode(status_code, data);
    }

    [HttpGet("infrastructure/health")]
    public async Task<IActionResult> GetServiceHealth()
    {
        var (status_code, data) = await _healthService.GetServiceHealth();
        return StatusCode(status_code, data);
    }

    [HttpGet("infrastructure/db-pools")]
    public async Task<IActionResult> GetDbPools()
    {
        var (status_code, data) = await _healthService.GetDbPools();
        return StatusCode(status_code, data);
    }

    [HttpPost("infrastructure/db-pools/test")]
    public async Task<IActionResult> TestDbConnection([FromBody] DbTestRequest request)
    {
        var (status_code, data) = await _healthService.TestDbConnection(request);
        return StatusCode(status_code, data);
    }

    [HttpGet("infrastructure/storage")]
    public async Task<IActionResult> GetStorageStats()
    {
        var (status_code, data) = await _configService.GetStorageStats();
        return StatusCode(status_code, data);
    }

    [HttpGet("infrastructure/secrets")]
    public async Task<IActionResult> GetSecrets()
    {
        var (status_code, data) = await _configService.GetSecrets();
        return StatusCode(status_code, data);
    }

    [HttpPost("infrastructure/backup")]
    public async Task<IActionResult> TriggerBackup()
    {
        var (status_code, data) = await _backupService.TriggerBackup();
        return StatusCode(status_code, data);
    }

    [HttpGet("settings/integrations")]
    public IActionResult GetIntegrationStatus()
    {
        var (status_code, data) = _configService.GetIntegrationStatus();
        return StatusCode(status_code, data);
    }

    [HttpGet("settings/platform-info")]
    public IActionResult GetPlatformInfo()
    {
        var (status_code, data) = _configService.GetPlatformInfo();
        return StatusCode(status_code, data);
    }

    [HttpGet("settings/webhooks")]
    public IActionResult GetWebhookConfig()
    {
        var (status_code, data) = _configService.GetWebhookConfig();
        return StatusCode(status_code, data);
    }

    [HttpGet("settings/general")]
    public IActionResult GetGeneralSettings()
    {
        var (status_code, data) = _configService.GetGeneralSettings();
        return StatusCode(status_code, data);
    }
}
