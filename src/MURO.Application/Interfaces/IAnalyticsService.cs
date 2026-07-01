using MURO.Application.DTOs.Analytics;

namespace MURO.Application.Interfaces;

/// <summary>
/// Fix #4: Interface Application katmanına taşındı (önceden Infrastructure'daydı — Clean Architecture ihlali).
/// </summary>
public interface IAnalyticsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync();
    Task<List<VideoWatchStatsDto>> GetVideoStatsAsync();
    Task<List<TransactionDto>> GetTransactionsAsync(DateTime from, DateTime to);
    Task<List<DeviceSessionDto>> GetActiveSessionsAsync();
    Task<List<DeviceSessionDto>> GetRecentSessionsAsync(int days = 7);
    Task<CourseAttendanceReportDto> GetCourseAttendanceReportAsync(Guid courseId);
    Task<StudentScorecardDto> GetStudentScorecardAsync(Guid studentId);
    Task<List<StudentScorecardDto>> GetStudentScorecardsListAsync();
    Task<StudentAcademicHistoryDto> GetStudentAcademicHistoryAsync(Guid studentId);
    Task<ScorecardSummaryDto> GetScorecardSummaryAsync();
    Task<StudentDashboardDto> GetStudentDashboardAsync(Guid userId);
    Task<AdminDashboardDto> GetAdminDashboardAsync();
}
