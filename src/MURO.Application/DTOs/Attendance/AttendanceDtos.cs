namespace MURO.Application.DTOs.Attendance;

public record SessionAttendanceDto(
    Guid Id,
    Guid UserId,
    string UserFullName,
    DateTime JoinedAt,
    DateTime? LeftAt,
    int? DurationMinutes,
    bool IsPresent
);

public record AttendanceSummaryDto(
    Guid SessionId,
    string SessionTitle,
    int TotalAssigned,
    int TotalPresent,
    double AttendanceRate,
    List<SessionAttendanceDto> Attendees
);

public record RecordAttendanceRequest(
    Guid SessionId,
    Guid UserId,
    DateTime JoinedAt
);

public record UpdateAttendanceRequest(
    DateTime LeftAt
);

public record MyAttendanceDto(
    Guid SessionId,
    string SessionTitle,
    string CourseTitle,
    DateTime JoinedAt,
    DateTime? LeftAt,
    int? DurationMinutes
);
