using MURO.Application.DTOs;
using MURO.Application.DTOs.Notifications;

namespace MURO.Application.Interfaces;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(Guid tenantId, Guid userId, int page, int pageSize, bool? unreadOnly);
    Task<NotificationDto> CreateAsync(Guid tenantId, CreateNotificationRequest request);
    Task<int> BulkSendAsync(Guid tenantId, BulkNotificationRequest request);
    Task MarkAsReadAsync(Guid notificationId);
    Task MarkAllReadAsync(Guid tenantId, Guid userId);
    Task<int> GetUnreadCountAsync(Guid tenantId, Guid userId);
    Task<List<NotificationAdminDto>> GetTenantSentAsync(Guid tenantId);
}
