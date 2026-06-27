namespace MURO.Application.DTOs.Courses;

public record CourseInstructorDto(Guid Id, string FullName, string Email);

public record CourseListDto(
    Guid Id,
    string Title,
    string? Description,
    string? ThumbnailUrl,
    string CourseType,
    bool IsPublished,
    int SessionCount,
    int GroupCount,
    int Order,
    DateTime? StartDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? InstructorId,
    string? InstructorName,
    List<CourseInstructorDto>? Instructors = null
);

public record CourseDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string? ThumbnailUrl,
    string CourseType,
    bool IsPublished,
    int Order,
    DateTime? StartDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<SessionDto> Sessions,
    List<CourseGroupDto> Groups,
    Guid? InstructorId,
    string? InstructorName,
    List<CourseInstructorDto>? Instructors = null
);

public record SessionDto(
    Guid Id,
    string Title,
    string? Description,
    int Order,
    string? VideoUrl,
    int? DurationMinutes,
    bool IsFree,
    DateTime? ScheduledStart,
    DateTime? ScheduledEnd,
    bool RecordingEnabled,
    string Status,            // Scheduled, Live, Ended
    string? BbbMeetingId,    // BBB meeting ID (canlı ders aktifken dolu)
    DateTime CreatedAt
);

// Fix #2: N+1 eliminasyonu için eklendi — Session + Course bilgisi tek sorguda
public record UpcomingSessionDto(
    Guid Id, string Title, string? Description, int Order,
    string? VideoUrl, int? DurationMinutes, bool IsFree,
    DateTime? ScheduledStart, DateTime? ScheduledEnd,
    bool RecordingEnabled, string Status, string? BbbMeetingId,
    DateTime CreatedAt,
    Guid CourseId, string CourseTitle
);

public record CourseGroupDto(
    Guid GroupId,
    string GroupName,
    string Mode
);

public record CreateCourseRequest(
    string Title,
    string? Description,
    string? ThumbnailUrl,
    string CourseType,
    int? Order,
    DateTime? StartDate,
    Guid? InstructorId,
    List<Guid>? InstructorIds = null
);

public record UpdateCourseRequest(
    string? Title,
    string? Description,
    string? ThumbnailUrl,
    string? CourseType,
    bool? IsPublished,
    int? Order,
    DateTime? StartDate,
    Guid? InstructorId,
    List<Guid>? InstructorIds = null
);

public record CreateSessionRequest(
    string Title,
    string? Description,
    int? Order,
    string? VideoUrl,
    int? DurationMinutes,
    bool IsFree,
    DateTime? ScheduledStart,
    DateTime? ScheduledEnd,
    bool RecordingEnabled = true
);

public record UpdateSessionRequest(
    string? Title,
    string? Description,
    int? Order,
    string? VideoUrl,
    int? DurationMinutes,
    bool? IsFree,
    DateTime? ScheduledStart,
    DateTime? ScheduledEnd,
    bool? RecordingEnabled
);

// Session start/end aksiyon sonuç DTO'ları
public record SessionStartResult(
    Guid SessionId,
    string BbbMeetingId,
    string ModeratorJoinUrl,  // Admin için hazır link
    string Status
);

public record SessionJoinResult(
    Guid SessionId,
    string JoinUrl,           // Frontend bu URL'e redirect eder
    bool IsModerator
);

public record AssignCourseToGroupRequest(
    Guid GroupId,
    string Mode  // Online / Offline / Both
);

public record CourseMaterialDto(
    Guid Id,
    string Title,
    string FileName,
    string FilePath,
    string ContentType,
    long FileSize,
    DateTime CreatedAt
);

public record CreateVodRequest(
    string Title,
    string FilePath,
    int? DurationSeconds
);

public record CourseStudentListDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    DateTime AssignedAt,
    DateTime? ExpiresAt
);

public record AssignCourseToStudentRequest(
    Guid UserId
);

public record UpdateStudentExpirationRequest(
    DateTime? ExpiresAt
);
