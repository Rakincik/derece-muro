using MURO.Application.DTOs.Webhooks;

namespace MURO.Application.Interfaces;

public interface IWebhookHandlerService
{
    Task<PurchaseWebhookResponse> HandlePurchaseAsync(PurchaseWebhookRequest request);
    Task<CancelWebhookResponse> HandleCancelAsync(CancelWebhookRequest request);
    Task HandleBbbMeetingEndedAsync(BbbEvent evt);
    Task HandleBbbRecordingReadyAsync(BbbEvent evt);
}
