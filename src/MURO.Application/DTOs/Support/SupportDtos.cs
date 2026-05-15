namespace MURO.Application.DTOs.Support;

public record TicketListDto(
    Guid Id, string Subject, string Status, string Priority,
    string Category, string UserFullName, int MessageCount, DateTime CreatedAt
);

public record TicketDetailDto(
    Guid Id, string Subject, string Body, string Status,
    string Priority, string Category,
    Guid UserId, string UserFullName,
    List<TicketMessageDto> Messages, DateTime CreatedAt
);

public record TicketMessageDto(
    Guid Id, Guid SenderId, string SenderName, string Body, DateTime CreatedAt
);

public record CreateTicketRequest(string Subject, string Body, string Priority, string Category);
public record ReplyTicketRequest(string Body);

public record FaqDto(Guid Id, string QuestionText, string AnswerText, string? Category, int SortOrder);
public record CreateFaqRequest(string QuestionText, string AnswerText, string? Category, int? SortOrder);
public record UpdateFaqRequest(string? QuestionText, string? AnswerText, string? Category, int? SortOrder);
