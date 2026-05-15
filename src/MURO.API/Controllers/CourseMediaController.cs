using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs.Media;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[ApiController]
[Authorize(Roles = "Superadmin,Admin,Instructor")]
[Route("api/v1/courses/{courseId}/media")]
public class CourseMediaController : ControllerBase
{
    private readonly ICourseMediaService _courseMediaService;

    public CourseMediaController(ICourseMediaService courseMediaService)
    {
        _courseMediaService = courseMediaService;
    }

    private Guid GetTenantId()
    {
        var tenantIdStr = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                          ?? User.FindFirst("tenantId")?.Value;
        return Guid.TryParse(tenantIdStr, out var tid) ? tid : Guid.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> GetCourseMedias(Guid courseId)
    {
        var medias = await _courseMediaService.GetCourseMediasAsync(GetTenantId(), courseId);
        return Ok(medias);
    }

    [HttpPost("assign")]
    public async Task<IActionResult> AssignMedia(Guid courseId, [FromBody] AssignMediaToCourseRequest request)
    {
        var result = await _courseMediaService.AssignMediaAsync(GetTenantId(), courseId, request);
        return Ok(result);
    }

    [HttpPost("bulk-assign-folder")]
    public async Task<IActionResult> BulkAssignFolder(Guid courseId, [FromBody] BulkAssignFolderToCourseRequest request)
    {
        await _courseMediaService.BulkAssignFolderAsync(GetTenantId(), courseId, request);
        return Ok();
    }

    [HttpDelete("{mediaAssetId}")]
    public async Task<IActionResult> RemoveMedia(Guid courseId, Guid mediaAssetId)
    {
        await _courseMediaService.RemoveMediaAsync(GetTenantId(), courseId, mediaAssetId);
        return NoContent();
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> ReorderMedias(Guid courseId, [FromBody] ReorderCourseMediaRequest request)
    {
        await _courseMediaService.ReorderMediasAsync(GetTenantId(), courseId, request);
        return Ok();
    }
}
