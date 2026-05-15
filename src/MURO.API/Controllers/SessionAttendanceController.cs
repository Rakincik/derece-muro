using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs.Attendance;
using MURO.Application.Interfaces;
using System.Security.Claims;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/attendance")]
[Authorize]
public class SessionAttendanceController : ControllerBase
{
    private readonly ISessionAttendanceService _attendanceService;
    private readonly ITenantService _tenantService;

    public SessionAttendanceController(ISessionAttendanceService attendanceService, ITenantService tenantService)
    {
        _attendanceService = attendanceService;
        _tenantService = tenantService;
    }

    // Eğitmen / Admin: Bir sınıf yoklamasının tüm özeti
    [HttpGet("sessions/{sessionId}")]
    public async Task<ActionResult<AttendanceSummaryDto>> GetSessionAttendance(Guid sessionId)
    {
        var tenantId = _tenantService.CurrentTenantId
            ?? throw new UnauthorizedAccessException("Tenant bulunamadı.");
        var result = await _attendanceService.GetAttendanceBySessionAsync(tenantId, sessionId);
        return Ok(result);
    }

    // Öğrenci: Kendi ders katılım geçmişim
    [HttpGet("my")]
    public async Task<ActionResult<List<MyAttendanceDto>>> GetMyAttendance()
    {
        var tenantId = _tenantService.CurrentTenantId
            ?? throw new UnauthorizedAccessException("Tenant bulunamadı.");
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _attendanceService.GetMyAttendanceHistoryAsync(tenantId, userId);
        return Ok(result);
    }
}
