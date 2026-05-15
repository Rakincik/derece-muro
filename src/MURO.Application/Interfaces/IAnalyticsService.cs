using MURO.Application.DTOs.Analytics;

namespace MURO.Application.Interfaces;

/// <summary>
/// Fix #4: Interface Application katmanına taşındı (önceden Infrastructure'daydı — Clean Architecture ihlali).
/// </summary>
public interface IAnalyticsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid tenantId);
    Task<List<VideoWatchStatsDto>> GetVideoStatsAsync(Guid tenantId);
    Task<List<TransactionDto>> GetTransactionsAsync(Guid tenantId, DateTime from, DateTime to);
    Task<List<DeviceSessionDto>> GetActiveSessionsAsync(Guid tenantId);
    Task<CourseAttendanceReportDto> GetCourseAttendanceReportAsync(Guid tenantId, Guid courseId);
    Task<StudentScorecardDto> GetStudentScorecardAsync(Guid tenantId, Guid studentId);
    Task<ScorecardSummaryDto> GetScorecardSummaryAsync(Guid tenantId);
    Task<StudentDashboardDto> GetStudentDashboardAsync(Guid tenantId, Guid userId);
}
