using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs.Calendar;
using MURO.Application.Interfaces;
using System.Security.Claims;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/student")]
[Authorize]
public class StudentController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly ITenantService _tenantService;

    public StudentController(IStudentService studentService, ITenantService tenantService)
    {
        _studentService = studentService;
        _tenantService = tenantService;
    }

    private Guid GetTenantId() =>
        _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException("Kurum bilgisi bulunamadı.");

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException("Kullanıcı kimliği bulunamadı."));

    [HttpGet("calendar")]
    public async Task<ActionResult<List<CalendarEventDto>>> GetCalendar(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null)
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

        var events = await _studentService.GetCalendarEventsAsync(GetTenantId(), GetUserId(), start, end);
        return Ok(events);
    }
}
