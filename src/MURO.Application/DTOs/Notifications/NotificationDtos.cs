namespace MURO.Application.DTOs.Notifications;

public record NotificationDto(
    Guid Id, string Title, string Body, string? Type,
    bool IsRead, string Channel, DateTime CreatedAt
);

public record NotificationAdminDto(
    Guid Id, string Title, string Body, string? Type,
    DateTime CreatedAt, int RecipientCount
);

public record CreateNotificationRequest(
    Guid UserId, string Title, string Body, string? Type, string Channel
);

public record BulkNotificationRequest(
    List<Guid> UserIds, string Title, string Body, string? Type,
    Guid? GroupId = null, bool SendToAll = false, DateTime? ScheduledAt = null
);
