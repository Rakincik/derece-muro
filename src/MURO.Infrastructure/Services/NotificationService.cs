using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs;
using MURO.Application.DTOs.Notifications;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly MuroDbContext _context;
    private readonly INotificationPush _push;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;
    private readonly ICacheService _cache;

    public NotificationService(
        MuroDbContext context,
        INotificationPush push,
        IEmailSender emailSender,
        ISmsSender smsSender,
        ICacheService cache)
    {
        _context     = context;
        _push        = push;
        _emailSender = emailSender;
        _smsSender   = smsSender;
        _cache       = cache;
    }

    // ─── Kullanıcı: Bildirim Listesi ────────────────────────────────────────

    public async Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(
        Guid tenantId, Guid userId, int page, int pageSize, bool? unreadOnly)
    {
        var cacheKey = $"{tenantId}:notifications:user:{userId}:{page}:{pageSize}:{unreadOnly}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.Notifications.AsNoTracking()
                .Where(n => n.UserId == userId && n.TenantId == tenantId);

            if (unreadOnly == true) query = query.Where(n => !n.IsRead);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query.OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.Type, n.IsRead, n.Channel.ToString(), n.CreatedAt))
                .ToListAsync();

            return new PagedResult<NotificationDto>(items, totalCount, page, pageSize, totalPages);
        }, TimeSpan.FromMinutes(2));
    }

    // ─── Admin: Tekil Gönderim ───────────────────────────────────────────────

    public async Task<NotificationDto> CreateAsync(Guid tenantId, CreateNotificationRequest request)
    {
        var channel = Enum.TryParse<NotificationChannel>(request.Channel, true, out var ch)
            ? ch : NotificationChannel.System;

        var notification = new Notification
        {
            Id       = Guid.NewGuid(),
            TenantId = tenantId,
            UserId   = request.UserId,
            Title    = request.Title,
            Body     = request.Body,
            Type     = request.Type,
            Channel  = channel
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:notifications:");

        var dto = new NotificationDto(
            notification.Id, notification.Title, notification.Body,
            notification.Type, notification.IsRead, notification.Channel.ToString(), notification.CreatedAt);

        // ── SignalR push ──────────────────────────────────────────────────────
        await _push.PushToUserAsync(request.UserId.ToString(), dto);

        // ── E-posta / SMS ─────────────────────────────────────────────────────
        await DispatchExternalChannelAsync(request.UserId, notification.Title, notification.Body);

        return dto;
    }

    // ─── Admin: Toplu Gönderim ───────────────────────────────────────────────

    public async Task<int> BulkSendAsync(Guid tenantId, BulkNotificationRequest request)
    {
        List<Guid> userIds;

        if (request.SendToAll)
        {
            userIds = await _context.TenantMemberships.AsNoTracking()
                .Where(tm => tm.TenantId == tenantId && tm.Status == "active")
                .Select(tm => tm.UserId)
                .ToListAsync();
        }
        else if (request.GroupId.HasValue)
        {
            userIds = await _context.GroupMembers.AsNoTracking()
                .Where(gm => gm.GroupId == request.GroupId.Value)
                .Select(gm => gm.UserId)
                .ToListAsync();
        }
        else
        {
            userIds = request.UserIds;
        }

        if (userIds.Count == 0) return 0;

        var notifications = userIds.Select(uid => new Notification
        {
            Id       = Guid.NewGuid(),
            TenantId = tenantId,
            UserId   = uid,
            Title    = request.Title,
            Body     = request.Body,
            Type     = request.Type,
            Channel  = NotificationChannel.System
        }).ToList();

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"{tenantId}:notifications:");

        // ── SignalR push: her kullanıcıya kendi grubundan ────────────────────
        var pushTasks = notifications.Select(n =>
        {
            var dto = new NotificationDto(n.Id, n.Title, n.Body, n.Type, n.IsRead, n.Channel.ToString(), n.CreatedAt);
            return _push.PushToUserAsync(n.UserId.ToString(), dto);
        });
        await Task.WhenAll(pushTasks);

        return notifications.Count;
    }

    // ─── Okundu İşlemleri ────────────────────────────────────────────────────

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var n = await _context.Notifications.FindAsync(notificationId);
        if (n != null)
        {
            n.IsRead = true;
            await _context.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync($"{n.TenantId}:notifications:");
        }
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        // Tenant bilgisini kullanıcının ilk bildiriminden al
        var tenantId = await _context.Notifications
            .Where(n => n.UserId == userId)
            .Select(n => n.TenantId)
            .FirstOrDefaultAsync();

        await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        if (tenantId != default)
            await _cache.RemoveByPrefixAsync($"{tenantId}:notifications:");
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        // Tenant-safe cache key: userId'den tenant'ı resolve et
        var tenantId = await _context.Notifications
            .Where(n => n.UserId == userId)
            .Select(n => n.TenantId)
            .FirstOrDefaultAsync();

        var cacheKey = $"{tenantId}:notifications:unread:{userId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        }, TimeSpan.FromMinutes(1));
    }

    // ─── Admin: Gönderim Geçmişi ─────────────────────────────────────────────

    public async Task<List<NotificationAdminDto>> GetTenantSentAsync(Guid tenantId)
    {
        var rows = await _context.Notifications.AsNoTracking()
            .Where(n => n.TenantId == tenantId)
            .GroupBy(n => new { n.Title, n.Body, n.Type, Day = n.CreatedAt.Date })
            .Select(g => new NotificationAdminDto(
                g.Min(n => n.Id),
                g.Key.Title,
                g.Key.Body ?? string.Empty,
                g.Key.Type,
                g.Max(n => n.CreatedAt),
                g.Count()
            ))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        return rows;
    }

    // ─── Yardımcı Metodlar ───────────────────────────────────────────────────

    private async Task DispatchExternalChannelAsync(Guid userId, string title, string body)
    {
        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, Phone = u.Phone, u.FirstName, u.LastName })
            .FirstOrDefaultAsync();

        if (user == null) return;

        var htmlBody = $@"
            <div style='font-family:sans-serif;max-width:600px;margin:auto'>
              <h2 style='color:#4F46E5'>{title}</h2>
              <p>{body}</p>
              <hr style='border:none;border-top:1px solid #eee;margin-top:24px'/>
              <p style='font-size:12px;color:#999'>MURO — Otomatik bildirim</p>
            </div>";

        await _emailSender.SendAsync(
            user.Email, $"{user.FirstName} {user.LastName}",
            title, htmlBody);

        if (!string.IsNullOrWhiteSpace(user.Phone))
            await _smsSender.SendAsync(user.Phone, $"{title}: {body}");
    }
}
