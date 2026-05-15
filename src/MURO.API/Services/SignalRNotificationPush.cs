using Microsoft.AspNetCore.SignalR;
using MURO.API.Hubs;
using MURO.Application.DTOs.Notifications;
using MURO.Application.Interfaces;

namespace MURO.API.Services;

/// <summary>
/// INotificationPush'un SignalR implementasyonu. MURO.API katmanındadır
/// ve IHubContext'e bağımlıdır — Infrastructure bağımlılık yaratmaz.
/// </summary>
public class SignalRNotificationPush : INotificationPush
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationPush(IHubContext<NotificationHub> hub)
        => _hub = hub;

    public Task PushToUserAsync(string userId, NotificationDto dto)
        => _hub.Clients.Group(userId).SendAsync("ReceiveNotification", dto);
}
