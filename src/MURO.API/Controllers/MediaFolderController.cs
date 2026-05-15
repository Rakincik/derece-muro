using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs.Media;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[ApiController]
[Route("api/v1/media-folders")]
[Authorize(Roles = "Superadmin,Admin,Instructor")]
public class MediaFolderController : ControllerBase
{
    private readonly IMediaFolderService _folderService;

    public MediaFolderController(IMediaFolderService folderService)
    {
        _folderService = folderService;
    }

    private Guid GetTenantId()
    {
        var tenantIdStr = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                          ?? User.FindFirst("tenantId")?.Value;
        return Guid.TryParse(tenantIdStr, out var tid) ? tid : Guid.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> GetFolders([FromQuery] Guid? parentFolderId = null)
    {
        var folders = await _folderService.GetFoldersAsync(GetTenantId(), parentFolderId);
        return Ok(folders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetFolder(Guid id)
    {
        var folder = await _folderService.GetFolderByIdAsync(GetTenantId(), id);
        return Ok(folder);
    }

    [HttpPost]
    public async Task<IActionResult> CreateFolder([FromBody] CreateMediaFolderRequest request)
    {
        var folder = await _folderService.CreateFolderAsync(GetTenantId(), request);
        return Ok(folder);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFolder(Guid id, [FromBody] UpdateMediaFolderRequest request)
    {
        var folder = await _folderService.UpdateFolderAsync(GetTenantId(), id, request);
        return Ok(folder);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFolder(Guid id)
    {
        await _folderService.DeleteFolderAsync(GetTenantId(), id);
        return NoContent();
    }
}
