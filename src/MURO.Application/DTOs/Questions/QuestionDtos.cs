namespace MURO.Application.DTOs.Questions;

public record QuestionDto(
    Guid Id, Guid UserId, string UserFullName,
    Guid InstructorId, string InstructorFullName,
    string Subject, string Body, string? ImageUrl, string? AudioUrl,
    Guid? CourseId, string? CourseTitle,
    string? Answer, DateTime? AnsweredAt,
    string Status, DateTime CreatedAt,
    string? Note
);

public record CreateQuestionRequest(
    Guid InstructorId, string Subject, string Body,
    string? ImageUrl, string? AudioUrl, string? Note,
    Guid? CourseId
);

public record AnswerQuestionRequest(string Answer);

public record UpdateNoteRequest(string? Note);
