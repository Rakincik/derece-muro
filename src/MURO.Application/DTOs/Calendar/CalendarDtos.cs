namespace MURO.Application.DTOs.Calendar;

public record CalendarEventDto(
    Guid Id,
    string Title,
    string? Description,
    string EventType,
    DateTime StartDate,
    DateTime EndDate,
    Guid? GroupId,
    string? GroupName,
    Guid? CourseId,
    string? CourseName,
    string? Color,
    DateTime CreatedAt
);

public record CreateCalendarEventRequest(
    string Title,
    string? Description,
    string EventType,
    DateTime StartDate,
    DateTime EndDate,
    Guid? GroupId,
    Guid? CourseId,
    string? Color
);

public record UpdateCalendarEventRequest(
    string? Title,
    string? Description,
    string? EventType,
    DateTime? StartDate,
    DateTime? EndDate,
    Guid? GroupId,
    Guid? CourseId,
    string? Color
);
