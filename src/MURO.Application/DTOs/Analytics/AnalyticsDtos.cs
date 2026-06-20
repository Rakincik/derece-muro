namespace MURO.Application.DTOs.Analytics;

public record DashboardStatsDto(
    int TotalUsers, int ActiveStudents, int DemoStudents,
    int TotalCourses, int PublishedCourses,
    int TotalExams, int TotalAssignments,
    int TotalGroups, int PendingTickets
);

public record VideoWatchStatsDto(
    Guid MediaAssetId, string MediaTitle,
    int TotalViews, int UniqueViewers,
    double AvgWatchPercentage, int CompletionCount,
    double AvgSkipCount, double AvgReplayCount
);

public record TransactionDto(
    Guid Id, string Description, decimal Amount,
    string Type, DateTime TransactionDate
);

public record DeviceSessionDto(
    Guid Id, Guid UserId, string UserFullName,
    string DeviceInfo, string? IpAddress,
    DateTime LoginAt, DateTime? LogoutAt, bool IsActive
);

// ── Admin: Ders bazlı yoklama raporu ─────────────────────────────────────
public record CourseAttendanceReportDto(
    Guid CourseId,
    string CourseTitle,
    int TotalSessions,
    int TotalEnrolled,
    double AvgAttendanceRate,
    int RiskStudentCount,
    List<SessionAttendanceSummaryDto> Sessions,
    List<EnrolledStudentDto> EnrolledStudents
);

public record EnrolledStudentDto(
    Guid UserId,
    string FullName
);

public record SessionAttendanceSummaryDto(
    Guid SessionId,
    string SessionTitle,
    DateTime? ScheduledStart,
    int PresentCount,
    int TotalEnrolled,
    double AttendanceRate,
    List<Guid> PresentStudentIds
);

// ── Admin: Öğrenci skor kartı (tek öğrenci) ──────────────────────────────
public record StudentScorecardDto(
    Guid UserId,
    string FullName,
    string Email,
    int AttendedSessions,
    int TotalSessions,
    double AttendanceRate,
    int CompletedVideos,
    int TotalVideos,
    double VideoCompletionRate,
    int TotalWatchedMinutes,
    int SubmittedAssignments,
    double AvgExamScore
);

public record StudentAcademicHistoryDto(
    List<StudentExamHistoryDto> Exams,
    List<StudentAssignmentHistoryDto> Assignments
);

public record StudentExamHistoryDto(
    Guid ExamId,
    string Title,
    double Score,
    double Net,
    DateTime TakenAt
);

public record StudentAssignmentHistoryDto(
    Guid AssignmentId,
    string Title,
    string Status,
    int? Grade,
    DateTime SubmittedAt
);

// ── Admin: Tüm öğrencilerin toplu karne ortalaması ───────────────────────
public record ScorecardSummaryDto(
    int TotalStudents,
    double AvgAttendanceRate,
    double AvgVideoCompletionRate,
    double AvgExamScore,
    int AvgAttendedSessions,
    int AvgTotalSessions,
    int AvgCompletedVideos,
    int AvgTotalVideos,
    int AvgTotalWatchedMinutes,
    int AvgSubmittedAssignments
);

// ── Öğrenci Dashboard ─────────────────────────────────────────────────────
public record StudentDashboardDto(
    int TotalWatchedMinutes,      // Bu hafta çalışılan dakika
    int AttendedSessionsThisMonth,
    int TotalSessionsThisMonth,
    double AttendanceRate,
    int CompletedVideos,
    int ConsecutiveDays,           // Ardışık günlerde giriş (aktif seri)
    double AvgExamNet,                      // Sınav net ortalaması
    List<ResumeVideoDto> ContinueWatching,  // Yarıda bırakılan videolar
    List<WeeklyActivityDto> WeeklyActivity  // Son 7 gün aktivite
);

public record ResumeVideoDto(
    Guid MediaAssetId,
    string Title,
    string? ThumbnailPath,
    int LastPosition,
    int TotalSeconds,
    double CompletionPercentage
);

public record WeeklyActivityDto(
    string DayLabel,     // "Pzt", "Sal", etc.
    int Minutes,         // O gün izlenen dakika
    bool IsToday
);

// ── BFF (Backend For Frontend) ──────────────────────────────────────────
public record StudentDashboardSummaryDto(
    StudentDashboardDto Stats,
    List<MURO.Application.DTOs.Courses.CourseListDto> Courses,
    List<MURO.Application.DTOs.Courses.UpcomingSessionDto> UpcomingSessions,
    int UnreadNotificationCount
);

// ── Admin Dashboard (Charts & KPIs) ───────────────────────────────────────
public record AdminDashboardDto(
    int TotalVideosWatched,
    List<AdminWeeklyActivityDto> WeeklyActivity,
    List<AdminTopCourseDto> TopCourses,
    List<AdminTopStudentDto> TopStudents
);

public record AdminWeeklyActivityDto(
    string Day,
    int VideoMinutes,
    int Sessions
);

public record AdminTopCourseDto(
    Guid CourseId,
    string Title,
    int StudentCount,
    double AvgAttendance
);

public record AdminTopStudentDto(
    Guid UserId,
    string FullName,
    double Score
);

