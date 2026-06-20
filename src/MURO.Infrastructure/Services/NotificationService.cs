using System.Text.Json;
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
    private readonly IJobQueue _jobQueue;

    public NotificationService(
        MuroDbContext context,
        INotificationPush push,
        IEmailSender emailSender,
        ISmsSender smsSender,
        ICacheService cache,
        IJobQueue jobQueue)
    {
        _context     = context;
        _push        = push;
        _emailSender = emailSender;
        _smsSender   = smsSender;
        _cache       = cache;
        _jobQueue    = jobQueue;
    }

    // ─── Kullanıcı: Bildirim Listesi ────────────────────────────────────────

    public async Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(
        Guid userId, int page, int pageSize, bool? unreadOnly)
    {
        var cacheKey = $"notifications:user:{userId}:{page}:{pageSize}:{unreadOnly}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var query = _context.Notifications.AsNoTracking()
                .Where(n => n.UserId == userId );

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

    public async Task<NotificationDto> CreateAsync(CreateNotificationRequest request)
    {
        var channel = Enum.TryParse<NotificationChannel>(request.Channel, true, out var ch)
            ? ch : NotificationChannel.System;

        var payload = new NotificationJobPayload
        {
            UserIds = new List<Guid> { request.UserId },
            Title = request.Title ?? string.Empty,
            Body = request.Body ?? string.Empty,
            Type = request.Type ?? string.Empty,
            Channel = channel.ToString()
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        await _jobQueue.EnqueueAsync("notifications", jsonPayload);

        // API'nin beklemesini engellemek için anında DTO dönüyoruz. (Id arka planda oluşacak)
        return new NotificationDto(Guid.NewGuid(), request.Title ?? string.Empty, request.Body ?? string.Empty, request.Type, false, channel.ToString(), DateTime.UtcNow);
    }

    // ─── Admin: Toplu Gönderim ───────────────────────────────────────────────

    public async Task<int> BulkSendAsync(BulkNotificationRequest request)
    {
        List<Guid> userIds;

        if (request.SendToAll)
        {
            userIds = await _context.Users.AsNoTracking()
                .Where(tm => tm.IsActive && tm.Role == Domain.Enums.UserRole.Student)
                .Select(tm => tm.Id)
                .ToListAsync();
        }
        else if (request.GroupId.HasValue)
        {
            userIds = await _context.GroupMembers.AsNoTracking()
                .Include(gm => gm.User)
                .Where(gm => gm.GroupId == request.GroupId.Value && gm.User.Role == Domain.Enums.UserRole.Student)
                .Select(gm => gm.UserId)
                .ToListAsync();
        }
        else if (request.CourseId.HasValue)
        {
            var groupIds = await _context.CourseGroups.AsNoTracking()
                .Where(cg => cg.CourseId == request.CourseId.Value)
                .Select(cg => cg.GroupId)
                .ToListAsync();
                
            userIds = await _context.GroupMembers.AsNoTracking()
                .Include(gm => gm.User)
                .Where(gm => groupIds.Contains(gm.GroupId) && gm.User.Role == Domain.Enums.UserRole.Student)
                .Select(gm => gm.UserId)
                .Distinct()
                .ToListAsync();
        }
        else
        {
            userIds = request.UserIds;
        }

        if (userIds.Count == 0) return 0;

        var payload = new NotificationJobPayload
        {
            UserIds = userIds,
            Title = request.Title ?? string.Empty,
            Body = request.Body ?? string.Empty,
            Type = request.Type ?? string.Empty,
            Channel = NotificationChannel.System.ToString()
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        await _jobQueue.EnqueueAsync("notifications", jsonPayload);

        return userIds.Count;
    }

    // ─── Okundu İşlemleri ────────────────────────────────────────────────────

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var n = await _context.Notifications.FindAsync(notificationId);
        if (n != null)
        {
            n.IsRead = true;
            await _context.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync($"notifications:");
        }
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        await _cache.RemoveByPrefixAsync($"notifications:");
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        var cacheKey = $"notifications:unread:{userId}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            return await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        }, TimeSpan.FromMinutes(1));
    }

    // ─── Admin: Gönderim Geçmişi ─────────────────────────────────────────────

    public async Task<List<NotificationAdminDto>> GetTenantSentAsync()
    {
        var rows = await _context.Notifications.AsNoTracking()
            .Where(n => true)
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
