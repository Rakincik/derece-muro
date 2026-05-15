using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs.Videos;
using MURO.Application.Interfaces;
using System.Security.Claims;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/videos")]
[Authorize]
public class VideoController : ControllerBase
{
    private readonly IVideoNoteService _noteService;
    private readonly IVideoProgressService _progressService;
    private readonly IBackgroundJobQueue _jobQueue;

    public VideoController(IVideoNoteService noteService, IVideoProgressService progressService, IBackgroundJobQueue jobQueue)
    {
        _noteService = noteService;
        _progressService = progressService;
        _jobQueue = jobQueue;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ── Progress (Kaldığım Yere Devam) ─────────────────────────────────────

    /// GET /api/v1/videos/{id}/progress  — Player açılınca lastPosition'ı çek
    [HttpGet("{mediaAssetId:guid}/progress")]
    public async Task<ActionResult<VideoProgressDto>> GetProgress(Guid mediaAssetId)
    {
        var result = await _progressService.GetProgressAsync(CurrentUserId, mediaAssetId);
        return result == null ? NoContent() : Ok(result);
    }

    /// PUT /api/v1/videos/{id}/progress  — Heartbeat ve konumu kaydet
    [HttpPut("{mediaAssetId:guid}/progress")]
    public async Task<ActionResult<VideoProgressDto>> UpdateProgress(
        Guid mediaAssetId, [FromBody] UpdateVideoProgressRequest request)
    {
        var result = await _progressService.UpsertProgressAsync(CurrentUserId, mediaAssetId, request);
        return result == null ? NotFound() : Ok(result);
    }

    /// GET /api/v1/videos/courses/{courseId}/progress  — Kurs genel ilerleme
    [HttpGet("courses/{courseId:guid}/progress")]
    public async Task<ActionResult<List<VideoProgressDto>>> GetCourseProgress(Guid courseId)
    {
        var result = await _progressService.GetMyCourseProgressAsync(CurrentUserId, courseId);
        return Ok(result);
    }

    // ── Notes (Zaman Damgalı Notlar) ────────────────────────────────────────

    /// GET /api/v1/videos/{id}/notes  — Öğrencinin notlarını getir
    [HttpGet("{mediaAssetId:guid}/notes")]
    public async Task<ActionResult<List<VideoNoteDto>>> GetNotes(Guid mediaAssetId)
    {
        var result = await _noteService.GetMyNotesAsync(CurrentUserId, mediaAssetId);
        return Ok(result);
    }

    /// POST /api/v1/videos/{id}/notes  — Yeni not ekle
    [HttpPost("{mediaAssetId:guid}/notes")]
    public async Task<ActionResult<VideoNoteDto>> AddNote(
        Guid mediaAssetId, [FromBody] CreateVideoNoteRequest request)
    {
        var result = await _noteService.AddNoteAsync(CurrentUserId, mediaAssetId, request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(null, CurrentUserId, null, "Create", "VideoNote", result.Id.ToString(), null, $"MediaAsset: {mediaAssetId}", GetIp()));
        return CreatedAtAction(nameof(GetNotes), new { mediaAssetId }, result);
    }

    /// PUT /api/v1/videos/notes/{noteId}  — Notu güncelle
    [HttpPut("notes/{noteId:guid}")]
    public async Task<ActionResult<VideoNoteDto>> UpdateNote(
        Guid noteId, [FromBody] UpdateVideoNoteRequest request)
    {
        var result = await _noteService.UpdateNoteAsync(CurrentUserId, noteId, request);
        return Ok(result);
    }

    /// DELETE /api/v1/videos/notes/{noteId}  — Notu sil
    [HttpDelete("notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid noteId)
    {
        await _noteService.DeleteNoteAsync(CurrentUserId, noteId);
        await _jobQueue.EnqueueAsync(new AuditLogJob(null, CurrentUserId, null, "Delete", "VideoNote", noteId.ToString(), null, null, GetIp()));
        return NoContent();
    }
}
