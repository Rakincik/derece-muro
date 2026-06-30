using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly MuroDbContext _context;
    private readonly ICacheService _cache;

    public AuditService(MuroDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task LogAsync(Guid? userId, string? userName, string action,
                               string entityType, string? entityId, string? entityName,
                               string? details = null, string? ipAddress = null)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            Details = details,
            IpAddress = ipAddress,
        });
        await _context.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync($"audit:");
    }

    public async Task<PagedResult<AuditLogDto>> GetLogsAsync(int page, int pageSize,
                                                              string? action = null, string? entityType = null,
                                                              string? search = null, DateTime? from = null, DateTime? to = null)
    {
        var cacheKey = $"audit:logs:{page}:{pageSize}:{action}:{entityType}:{search}:{from:yyyyMMddHHmm}:{to:yyyyMMddHHmm}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var q = _context.AuditLogs.AsNoTracking()
                .Where(a => true);

            if (!string.IsNullOrEmpty(action))
                q = q.Where(a => a.Action == action);
            if (!string.IsNullOrEmpty(entityType))
                q = q.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrEmpty(search))
            {
                if (Guid.TryParse(search, out var uid))
                {
                    q = q.Where(a => a.UserId == uid);
                }
                else
                {
                    var searchLower = search.ToLower();
                    q = q.Where(a => 
                        (a.UserName != null && a.UserName.ToLower().Contains(searchLower)) ||
                        (a.EntityName != null && a.EntityName.ToLower().Contains(searchLower)) ||
                        (a.IpAddress != null && a.IpAddress.ToLower().Contains(searchLower)) ||
                        (a.Details != null && a.Details.ToLower().Contains(searchLower))
                    );
                }
            }
            if (from.HasValue)
                q = q.Where(a => a.CreatedAt >= from.Value);
            if (to.HasValue)
                q = q.Where(a => a.CreatedAt <= to.Value);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogDto(
                    a.Id, a.UserId, a.UserName ?? (a.User != null ? a.User.FirstName + " " + a.User.LastName : null),
                    a.Action, a.EntityType, a.EntityId, a.EntityName,
                    a.Details, a.IpAddress, a.CreatedAt))
                .ToListAsync();

            return new PagedResult<AuditLogDto>(items, total, page, pageSize,
                (int)Math.Ceiling(total / (double)pageSize));
        }, TimeSpan.FromMinutes(1));
    }

    public async Task<AuditSummaryDto> GetSummaryAsync(DateTime from, DateTime to)
    {
        var cacheKey = $"audit:summary:{from:yyyyMMdd}:{to:yyyyMMdd}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var logs = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
                .ToListAsync();

            var createCount = logs.Count(l => l.Action == "Create");
            var updateCount = logs.Count(l => l.Action == "Update");
            var deleteCount = logs.Count(l => l.Action == "Delete");
            var nightCount = logs.Count(l => l.CreatedAt.Hour >= 22 || l.CreatedAt.Hour < 6);

            var topEntities = logs
                .GroupBy(l => l.EntityType)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Count());

            return new AuditSummaryDto(
                logs.Count, createCount, updateCount, deleteCount, nightCount, topEntities
            );
        }, TimeSpan.FromMinutes(2));
    }

    public async Task<PagedResult<UserAuditSummaryDto>> GetUserAuditSummariesAsync(int page, int pageSize, string? search = null, string? sortBy = null)
    {
        var q = _context.AuditLogs.AsNoTracking()
            .Where(a => a.UserId != null); // FIX: Sistem / Anonim (null UserId) kayıtlarını kullanıcı listesinden çıkarıyoruz.

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            q = q.Where(a => a.UserName != null && a.UserName.ToLower().Contains(searchLower));
        }

        var grouped = q.GroupBy(a => a.UserId)
            .Select(g => new {
                UserId = g.Key,
                ActionCount = g.Count(),
                LastActionAt = g.Max(a => a.CreatedAt)
            });

        var total = await grouped.CountAsync();
        
        var query = grouped;
        if (sortBy == "count")
        {
            query = query.OrderByDescending(x => x.ActionCount);
        }
        else
        {
            query = query.OrderByDescending(x => x.LastActionAt);
        }

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = items.Where(x => x.UserId.HasValue).Select(x => x.UserId.Value).ToList();
        var users = await _context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var lastActions = await _context.AuditLogs.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId ?? Guid.Empty))
            .GroupBy(a => a.UserId)
            .Select(g => new {
                UserId = g.Key,
                LastAction = g.OrderByDescending(a => a.CreatedAt).Select(a => a.Action).FirstOrDefault(),
                LastEntity = g.OrderByDescending(a => a.CreatedAt).Select(a => a.EntityType).FirstOrDefault()
            }).ToDictionaryAsync(x => x.UserId, x => new { x.LastAction, x.LastEntity });

        var result = items.Select(x => {
            string userName = "Bilinmeyen Kullanıcı";
            string? email = null;
            string? avatar = null;
            string? lastAction = null;

            if (x.UserId.HasValue) {
                if (users.TryGetValue(x.UserId.Value, out var u)) {
                    userName = $"{u.FirstName} {u.LastName}".Trim();
                    if (string.IsNullOrEmpty(userName)) userName = u.Email ?? "Bilinmeyen Kullanıcı";
                    email = u.Email;
                    avatar = null;
                }
                if (lastActions.TryGetValue(x.UserId.Value, out var act)) {
                    lastAction = $"{act.LastAction}:{act.LastEntity}";
                }
            }

            return new UserAuditSummaryDto(
                x.UserId, userName, email, avatar, x.ActionCount, x.LastActionAt, lastAction
            );
        }).ToList();

        return new PagedResult<UserAuditSummaryDto>(result, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<List<SuspiciousUserDto>> GetSuspiciousUsersAsync()
    {
        // Get suspicious security events within the last 7 days
        var thresholdDate = DateTime.UtcNow.AddDays(-7);
        var suspiciousEvents = await _context.SecurityEvents.AsNoTracking()
            .Include(e => e.User)
            .Where(e => e.CreatedAt >= thresholdDate && 
                        e.UserId != null &&
                        (e.EventType == "SESSION_KICKED" || e.EventType == "BRUTE_FORCE_DETECTED" || e.EventType == "LOGIN_FAILED"))
            .ToListAsync();

        // We only flag users who have SESSION_KICKED, BRUTE_FORCE, or >=5 LOGIN_FAILED
        var grouped = suspiciousEvents
            .GroupBy(e => new { e.UserId, UserName = e.User != null ? e.User.FirstName + " " + e.User.LastName : "Bilinmeyen Kullanıcı", e.EventType })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.UserName,
                g.Key.EventType,
                EventCount = g.Count(),
                LastEventAt = g.Max(e => e.CreatedAt)
            })
            .Where(x => x.EventType != "LOGIN_FAILED" || x.EventCount >= 5) // Only flag if multiple failures
            .OrderByDescending(x => x.LastEventAt)
            .Take(20)
            .Select(x => new SuspiciousUserDto(x.UserId, x.UserName, x.EventType, x.EventCount, x.LastEventAt))
            .ToList();

        return grouped;
    }
}
