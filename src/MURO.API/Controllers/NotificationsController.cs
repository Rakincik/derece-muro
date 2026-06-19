using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Notifications;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly Application.Interfaces.ITenantService _tenantService;
    private readonly IBackgroundJobQueue _jobQueue;

    public NotificationsController(INotificationService notificationService, Application.Interfaces.ITenantService tenantService, IBackgroundJobQueue jobQueue)
    {
        _notificationService = notificationService;
        _tenantService = tenantService;
        _jobQueue = jobQueue;
    }

    private Guid GetTenantId() => _tenantService.CurrentTenantId ?? throw new UnauthorizedAccessException();
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet("admin/sent")]
    public async Task<ActionResult<List<NotificationAdminDto>>> GetSentNotifications()
    {
        try
        {
            return Ok(await _notificationService.GetTenantSentAsync(GetTenantId()));
        }
        catch
        {
            return Ok(new List<NotificationAdminDto>());
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetMyNotifications(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? unreadOnly = null)
        => Ok(await _notificationService.GetUserNotificationsAsync(GetTenantId(), GetUserId(), page, pageSize, unreadOnly));

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
        => Ok(await _notificationService.GetUnreadCountAsync(GetTenantId(), GetUserId()));

    [HttpPost]
    public async Task<ActionResult<NotificationDto>> Create([FromBody] CreateNotificationRequest request)
    {
        var n = await _notificationService.CreateAsync(GetTenantId(), request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "Create", "Notification", null, request.Title, null, GetIp()));
        return Ok(n);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<int>> BulkSend([FromBody] BulkNotificationRequest request)
    {
        var count = await _notificationService.BulkSendAsync(GetTenantId(), request);
        await _jobQueue.EnqueueAsync(new AuditLogJob(GetTenantId(), GetUserId(), null, "BulkSend", "Notification", null, request.Title, $"{count} bildirim gönderildi", GetIp()));
        return Ok(count);
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    { await _notificationService.MarkAsReadAsync(id); return NoContent(); }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    { await _notificationService.MarkAllReadAsync(GetTenantId(), GetUserId()); return NoContent(); }
}
