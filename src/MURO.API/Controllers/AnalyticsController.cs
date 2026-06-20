using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs.Analytics;
using MURO.Application.Interfaces;
using System.Security.Claims;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    
    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
            }

    
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Admin Endpoints ──────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
    {
        try
        {
            return Ok(await _analyticsService.GetDashboardStatsAsync());
        }
        catch
        {
            return Ok(new DashboardStatsDto(0, 0, 0, 0, 0, 0, 0, 0, 0));
        }
    }

    [HttpGet("admin-dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> GetAdminDashboard()
    {
        try
        {
            return Ok(await _analyticsService.GetAdminDashboardAsync());
        }
        catch
        {
            return Ok(new AdminDashboardDto(0, new List<AdminWeeklyActivityDto>(), new List<AdminTopCourseDto>(), new List<AdminTopStudentDto>()));
        }
    }

    [HttpGet("video-stats")]
    public async Task<ActionResult<List<VideoWatchStatsDto>>> GetVideoStats()
    {
        try
        {
            return Ok(await _analyticsService.GetVideoStatsAsync());
        }
        catch
        {
            return Ok(new List<VideoWatchStatsDto>());
        }
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<List<TransactionDto>>> GetTransactions(
        [FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _analyticsService.GetTransactionsAsync(from, to));

    [HttpGet("sessions")]
    public async Task<ActionResult<List<DeviceSessionDto>>> GetActiveSessions()
        => Ok(await _analyticsService.GetActiveSessionsAsync());

    /// GET /api/v1/analytics/courses/{courseId}/attendance — Kurs devam raporu
    [HttpGet("courses/{courseId:guid}/attendance")]
    public async Task<ActionResult<CourseAttendanceReportDto>> GetCourseAttendance(Guid courseId)
        => Ok(await _analyticsService.GetCourseAttendanceReportAsync(courseId));

    /// GET /api/v1/analytics/students/{studentId}/scorecard — Öğrenci skor kartı
    [HttpGet("students/{studentId:guid}/scorecard")]
    public async Task<ActionResult<StudentScorecardDto>> GetStudentScorecard(Guid studentId)
        => Ok(await _analyticsService.GetStudentScorecardAsync(studentId));

    /// GET /api/v1/analytics/students/{studentId}/academic-history — Öğrenci akademik geçmiş
    [HttpGet("students/{studentId:guid}/academic-history")]
    public async Task<ActionResult<StudentAcademicHistoryDto>> GetStudentAcademicHistory(Guid studentId)
        => Ok(await _analyticsService.GetStudentAcademicHistoryAsync(studentId));

    /// GET /api/v1/analytics/students/summary — Tüm öğrencilerin toplu karne ortalaması
    [HttpGet("students/summary")]
    public async Task<ActionResult<ScorecardSummaryDto>> GetScorecardSummary()
        => Ok(await _analyticsService.GetScorecardSummaryAsync());    // ── Öğrenci Endpoint ─────────────────────────────────────────────────────

    /// GET /api/v1/analytics/me/dashboard — Öğrenci kendi dashboard istatistikleri
    [HttpGet("me/dashboard")]
    public async Task<ActionResult<StudentDashboardDto>> GetMyDashboard()
        => Ok(await _analyticsService.GetStudentDashboardAsync(GetUserId()));
}
