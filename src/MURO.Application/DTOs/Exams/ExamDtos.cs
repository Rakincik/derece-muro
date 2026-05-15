namespace MURO.Application.DTOs.Exams;

// ── Liste ──
public record ExamListDto(
    Guid Id,
    string Title,
    string? Description,
    string ExamType,
    int QuestionCount,
    int OptionCount,
    int? DurationMinutes,
    string Status,
    DateTime? StartDate,
    DateTime? EndDate,
    int AssignmentCount,
    int ResultCount,
    double? AverageScore,
    DateTime CreatedAt,
    double WrongPenaltyWeight = 0.25,
    string ResultMode = "immediate",
    DateTime? ResultPublishDate = null,
    double MaxScore = 0,
    int VirtualParticipantCount = 0
);

// ── Detay ──
public record ExamDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string ExamType,
    int QuestionCount,
    int OptionCount,
    int? DurationMinutes,
    string Status,
    bool ShowResults,
    string? PdfUrl,
    string? SolutionPdfUrl,
    Dictionary<int, string>? AnswerKey,
    DateTime? StartDate,
    DateTime? EndDate,
    DateTime CreatedAt,
    List<ExamAssignmentDto> Assignments,
    ExamResultSummaryDto? ResultSummary,
    double WrongPenaltyWeight = 0.25,
    string ResultMode = "immediate",
    DateTime? ResultPublishDate = null,
    Dictionary<int, double>? QuestionWeights = null,
    string? SectionsJson = null,
    double MaxScore = 0,
    int VirtualParticipantCount = 0
);

// ── Oluşturma ──
public record CreateExamRequest(
    string Title,
    string? Description,
    string ExamType,
    int QuestionCount,
    int OptionCount,
    int? DurationMinutes,
    DateTime? StartDate,
    DateTime? EndDate,
    bool ShowResults,
    double WrongPenaltyWeight = 0.25,
    string ResultMode = "immediate",
    DateTime? ResultPublishDate = null,
    Dictionary<int, double>? QuestionWeights = null,
    string? SectionsJson = null,
    double MaxScore = 0,
    int VirtualParticipantCount = 0
);

// ── Güncelleme ──
public record UpdateExamRequest(
    string? Title,
    string? Description,
    string? ExamType,
    int? QuestionCount,
    int? OptionCount,
    int? DurationMinutes,
    DateTime? StartDate,
    DateTime? EndDate,
    bool? ShowResults,
    double? WrongPenaltyWeight,
    string? ResultMode = null,
    DateTime? ResultPublishDate = null,
    Dictionary<int, double>? QuestionWeights = null,
    string? SectionsJson = null,
    double? MaxScore = null,
    int? VirtualParticipantCount = null
);

// ── Cevap Anahtarı ──
public record UpdateAnswerKeyRequest(
    Dictionary<int, string> AnswerKey   // {1:"A", 2:"C", 3:"B", ...}
);

// ── Durum Güncelleme ──
public record UpdateExamStatusRequest(
    string Status   // Taslak, Yayında, Tamamlandı
);

// ── PDF Güncelleme ──
public record UpdateExamPdfRequest(
    string? PdfUrl,
    string? SolutionPdfUrl
);

// ── Atama ──
public record ExamAssignmentDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string TargetName,
    DateTime? StartsAt,
    DateTime? EndsAt,
    DateTime AssignedAt
);

public record CreateExamAssignmentRequest(
    string TargetType,       // User, Group
    Guid TargetId,
    DateTime? StartsAt,
    DateTime? EndsAt
);

// ── Sonuçlar ──
public record ExamResultDto(
    Guid Id,
    Guid UserId,
    string UserFullName,
    int CorrectCount,
    int WrongCount,
    int EmptyCount,
    double Net,
    double Score,
    DateTime SubmittedAt,
    DateTime? StartedAt = null,
    int? DurationSeconds = null,
    Dictionary<string, SectionResultDto>? SectionResults = null
);

public record ExamResultSummaryDto(
    int TotalParticipants,
    double AverageScore,
    double AverageNet,
    double HighestScore,
    double LowestScore,
    List<ExamResultDto> Results,
    List<ScoreRangeDto>? ScoreDistribution = null
);

public record ScoreRangeDto(string Range, int Count);

public record ExamOverallSummaryDto(
    int ExamWithResultsCount,
    int TotalParticipants,
    double OverallAverageScore,
    int ExamTypeCount
);

// ── Öğrenci Optik Form Gönderimi ──
public record SubmitExamAnswersRequest(
    Dictionary<int, string> Answers,
    DateTime? StartedAt
);

// ── Öğrenci: Kendi sınav sonuçları ──
public record MyExamResultDto(
    Guid ExamId,
    string ExamTitle,
    string ExamType,
    int QuestionCount,
    int CorrectCount,
    int WrongCount,
    int EmptyCount,
    double Net,
    double Score,
    double? AverageScore,    // Sınıf ortalaması (showResults aktifse)
    int? Rank,               // Sınıftaki sırası
    DateTime SubmittedAt,
    bool ShowResults,
    Dictionary<string, SectionResultDto>? SectionResults = null
);

public record SectionResultDto(
    string Name,
    int CorrectCount,
    int WrongCount,
    int EmptyCount,
    double Net
);
