namespace MURO.Application.DTOs.Webhooks;

public record PurchaseWebhookRequest(
    string TenantCode,
    Guid PackageId,
    string UserEmail,
    string UserFirstName,
    string UserLastName,
    string? UserPhone,
    string? OrderId,
    DateTime PaidAt
);

public record CancelWebhookRequest(string OrderId, string Reason);

public class BbbWebhookPayload
{
    public List<BbbEvent> Events { get; set; } = new();
}

public class BbbEvent
{
    public string EventType { get; set; } = string.Empty;
    public string MeetingId { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string? SessionTitle { get; set; }
    public string? RecordingUrl { get; set; }
    public List<Guid>? EnrolledUserIds { get; set; }
}

public record PurchaseWebhookResponse(
    bool Success,
    Guid? UserId,
    Guid? UserPackageId,
    DateTime? ExpiresAt,
    string Message,
    bool Duplicate = false
);

public record CancelWebhookResponse(bool Success, string Message);
