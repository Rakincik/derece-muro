using MURO.Application.DTOs.Attendance;

namespace MURO.Application.Interfaces;

public interface ISessionAttendanceService
{
    Task<AttendanceSummaryDto> GetAttendanceBySessionAsync(Guid tenantId, Guid sessionId);
    Task<List<MyAttendanceDto>> GetMyAttendanceHistoryAsync(Guid tenantId, Guid userId);
    Task<SessionAttendanceDto> RecordJoinAsync(Guid tenantId, Guid sessionId, Guid userId);
    Task<SessionAttendanceDto> RecordLeaveAsync(Guid tenantId, Guid sessionId, Guid userId);
}
