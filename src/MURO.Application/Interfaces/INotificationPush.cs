using MURO.Application.DTOs.Notifications;

namespace MURO.Application.Interfaces;

/// <summary>
/// SignalR push için abstraction. Implementation MURO.API katmanında yapılır.
/// Infrastructure bu interface'e bağımlıdır, hub'a değil.
/// </summary>
public interface INotificationPush
{
    Task PushToUserAsync(string userId, NotificationDto dto);
}
