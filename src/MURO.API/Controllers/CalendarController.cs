using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs.Calendar;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/calendar")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendarService;
    private readonly ITenantService _tenantService;
    private readonly IBackgroundJobQueue _jobQueue;

    public CalendarController(ICalendarService calendarService, ITenantService tenantService, IBackgroundJobQueue jobQueue)
    {
        _calendarService = calendarService;
        _tenantService = tenantService;
        _jobQueue = jobQueue;
    }

    private Guid GetTenantId() =>
        _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException("Kurum bilgisi bulunamadı.");

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private Guid? GetInstructorIdIfInstructor()
    {
        if (User.IsInRole("Instructor") || User.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value == "Instructor"))
        {
            return GetUserId();
        }
        return null;
    }

    [HttpGet]
    public async Task<ActionResult<List<CalendarEventDto>>> GetEvents(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] Guid? groupId = null)
    {
        DateTime start, end;
        if (year.HasValue && month.HasValue)
        {
            start = new DateTime(year.Value, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            end = start.AddMonths(1).AddSeconds(-1);
        }
        else
        {
            start = from ?? DateTime.UtcNow.AddMonths(-1);
            end = to ?? DateTime.UtcNow.AddMonths(1);
        }
        return Ok(await _calendarService.GetEventsAsync(GetTenantId(), start, end, groupId, GetInstructorIdIfInstructor()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> GetEvent(Guid id)
    {
        return Ok(await _calendarService.GetEventByIdAsync(GetTenantId(), id));
    }

    [HttpPost]
    public async Task<ActionResult<CalendarEventDto>> CreateEvent([FromBody] CreateCalendarEventRequest request)
    {
        var ev = await _calendarService.CreateEventAsync(GetTenantId(), request, GetInstructorIdIfInstructor());
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Create", "CalendarEvent", ev.Id.ToString(), request.Title, null, GetIp()));
        return Created($"/api/v1/calendar/{ev.Id}", ev);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> UpdateEvent(Guid id, [FromBody] UpdateCalendarEventRequest request)
    {
        var ev = await _calendarService.UpdateEventAsync(GetTenantId(), id, request, GetInstructorIdIfInstructor());
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Update", "CalendarEvent", id.ToString(), request.Title, null, GetIp()));
        return Ok(ev);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        await _calendarService.DeleteEventAsync(GetTenantId(), id, GetInstructorIdIfInstructor());
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Delete", "CalendarEvent", id.ToString(), null, null, GetIp()));
        return NoContent();
    }
}
