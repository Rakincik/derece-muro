namespace MURO.Application.DTOs.Assignments;

public record AssignmentListDto(
    Guid Id,
    string Title,
    string? Description,
    Guid CourseId,
    string CourseName,
    DateTime DueDate,
    int SubmissionCount,
    DateTime CreatedAt
);

public record AssignmentDetailDto(
    Guid Id,
    string Title,
    string? Description,
    Guid CourseId,
    string CourseName,
    DateTime DueDate,
    string? FileUrl,
    int MaxScore,
    DateTime CreatedAt,
    List<SubmissionDto> Submissions
);

public record SubmissionDto(
    Guid Id,
    Guid UserId,
    string UserFullName,
    string? FileUrl,
    string? Comment,
    int? Score,
    string? Feedback,
    DateTime SubmittedAt
);

public record CreateAssignmentRequest(
    string Title,
    string? Description,
    Guid CourseId,
    DateTime DueDate,
    string? FileUrl,
    int MaxScore
);

public record UpdateAssignmentRequest(
    string? Title,
    string? Description,
    DateTime? DueDate,
    string? FileUrl,
    int? MaxScore
);

public record SubmitAssignmentRequest(
    string? FileUrl,
    string? Comment
);

public record GradeSubmissionRequest(
    int Score,
    string? Feedback
);

// ── Öğrenci ──────────────────────────────────────────────────────────────────
public record MyAssignmentDto(
    Guid Id,
    string Title,
    string? Description,
    string CourseName,
    DateTime DueDate,
    int MaxScore,
    string? FileUrl,           // Admin ek dosyası
    // Öğrencinin gönderimi (null → henüz göndermedi)
    Guid? SubmissionId,
    string? SubmissionFileUrl,
    string? SubmissionComment,
    int? Score,
    string? Feedback,
    DateTime? SubmittedAt,
    string Status              // "pending" | "submitted" | "graded" | "overdue"
);
