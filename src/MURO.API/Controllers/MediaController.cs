using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Media;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;
    private readonly Application.Interfaces.ITenantService _tenantService;
    private readonly IBackgroundJobQueue _jobQueue;

    public MediaController(IMediaService mediaService, Application.Interfaces.ITenantService tenantService, IBackgroundJobQueue jobQueue)
    {
        _mediaService = mediaService;
        _tenantService = tenantService;
        _jobQueue = jobQueue;
    }

    private Guid GetTenantId() => _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException();
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private bool IsStudent() => User.IsInRole("Student") || 
                                User.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value == "Student");

    // --- Student: Sadece grubunun kurslarına ait medya listesi ---
    [HttpGet]
    public async Task<ActionResult<PagedResult<MediaAssetDto>>> GetMyMedia(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] Guid? courseId = null, [FromQuery] string? search = null, [FromQuery] Guid? folderId = null)
    {
        // Student: grup filtreli — Admin/Instructor: tüm tenant medyaları
        Guid? userId = IsStudent() ? GetUserId() : null;
        return Ok(await _mediaService.GetAssetsAsync(GetTenantId(), page, pageSize, courseId, search, userId, folderId));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MediaAssetDto>> GetMediaById(Guid id)
    {
        // Student: kursuna erişim hakkı kontrolü — diğerleri bypass
        Guid? userId = IsStudent() ? GetUserId() : null;
        return Ok(await _mediaService.GetAssetByIdAsync(GetTenantId(), id, userId));
    }

    [HttpGet("{id:guid}/courses")]
    public async Task<ActionResult<List<Guid>>> GetMediaCourses(Guid id)
    {
        return Ok(await _mediaService.GetAssignedCourseIdsAsync(GetTenantId(), id));
    }

    [HttpGet("assets")]
    public async Task<ActionResult<PagedResult<MediaAssetDto>>> GetAssets(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] Guid? courseId = null, [FromQuery] Guid? folderId = null)
        => Ok(await _mediaService.GetAssetsAsync(GetTenantId(), page, pageSize, courseId, null, null, folderId, excludeRecordings: true));

    [HttpGet("transcode-progress")]
    public async Task<ActionResult<Dictionary<Guid, object>>> GetTranscodeProgress([FromQuery] string ids, [FromServices] ICacheService cache, [FromServices] MURO.Infrastructure.Persistence.MuroDbContext dbContext)
    {
        var dict = new Dictionary<Guid, object>();
        if (string.IsNullOrEmpty(ids)) return Ok(dict);
        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                        .Where(g => g != Guid.Empty).ToList();

        var statuses = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            System.Linq.Queryable.Select(
                System.Linq.Queryable.Where(dbContext.MediaAssets, m => idList.Contains(m.Id)),
                m => new { m.Id, m.Status }
            )
        );

        foreach(var stat in statuses)
        {
            var pr = await cache.GetAsync<MURO.Infrastructure.Services.TranscodeProgress>($"muro:upload:progress:{stat.Id}");
            dict[stat.Id] = new {
                percentage = pr?.Percentage ?? 0,
                speed = pr?.Speed ?? 0,
                etaSeconds = pr?.EtaSeconds ?? 0,
                status = stat.Status.ToString()
            };
        }
        return Ok(dict);
    }

    [HttpPost("assets")]
    public async Task<ActionResult<MediaAssetDto>> CreateAsset([FromBody] CreateMediaAssetRequest request)
    {
        var a = await _mediaService.CreateAssetAsync(GetTenantId(), request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Create", "MediaAsset", a.Id.ToString(), request.Title, null, GetIp()));
        return Created($"/api/v1/media/assets/{a.Id}", a);
    }

    [HttpPut("assets/{id:guid}")]
    public async Task<ActionResult<MediaAssetDto>> UpdateAsset(Guid id, [FromBody] UpdateMediaAssetRequest request)
        => Ok(await _mediaService.UpdateAssetAsync(GetTenantId(), id, request));

    [HttpDelete("assets/{id:guid}")]
    public async Task<IActionResult> DeleteAsset(Guid id)
    {
        await _mediaService.DeleteAssetAsync(GetTenantId(), id);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Delete", "MediaAsset", id.ToString(), null, null, GetIp()));
        return NoContent();
    }

    // --- Video Progress ---
    [HttpGet("progress/{mediaAssetId:guid}")]
    public async Task<ActionResult<VideoProgressDto>> GetProgress(Guid mediaAssetId)
        => Ok(await _mediaService.GetProgressAsync(GetUserId(), mediaAssetId));

    [HttpPut("progress/{mediaAssetId:guid}")]
    public async Task<ActionResult<VideoProgressDto>> UpdateProgress(Guid mediaAssetId, [FromBody] UpdateVideoProgressRequest request)
        => Ok(await _mediaService.UpdateProgressAsync(GetUserId(), mediaAssetId, request));

    // --- Podcasts ---
    [HttpGet("podcasts")]
    public async Task<ActionResult<PagedResult<PodcastDto>>> GetPodcasts(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediaService.GetPodcastsAsync(GetTenantId(), page, pageSize));

    [HttpPost("podcasts")]
    public async Task<ActionResult<PodcastDto>> CreatePodcast([FromBody] CreatePodcastRequest request)
    {
        var p = await _mediaService.CreatePodcastAsync(GetTenantId(), request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Create", "Podcast", p.Id.ToString(), request.Title, null, GetIp()));
        return Created($"/api/v1/media/podcasts/{p.Id}", p);
    }
}
