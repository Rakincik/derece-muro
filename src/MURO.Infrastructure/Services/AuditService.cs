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

    public async Task LogAsync(Guid? tenantId, Guid? userId, string? userName, string action,
                               string entityType, string? entityId, string? entityName,
                               string? details = null, string? ipAddress = null)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
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
        if (tenantId.HasValue)
            await _cache.RemoveByPrefixAsync($"{tenantId}:audit:");
    }

    public async Task<PagedResult<AuditLogDto>> GetLogsAsync(Guid tenantId, int page, int pageSize,
                                                              string? action = null, string? entityType = null,
                                                              string? search = null, DateTime? from = null, DateTime? to = null)
    {
        var cacheKey = $"{tenantId}:audit:logs:{page}:{pageSize}:{action}:{entityType}:{search}:{from:yyyyMMddHHmm}:{to:yyyyMMddHHmm}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var q = _context.AuditLogs.AsNoTracking()
                .Where(a => a.TenantId == tenantId);

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
                    a.Id, a.UserId, a.UserName,
                    a.Action, a.EntityType, a.EntityId, a.EntityName,
                    a.Details, a.IpAddress, a.CreatedAt))
                .ToListAsync();

            return new PagedResult<AuditLogDto>(items, total, page, pageSize,
                (int)Math.Ceiling(total / (double)pageSize));
        }, TimeSpan.FromMinutes(1));
    }

    public async Task<AuditSummaryDto> GetSummaryAsync(Guid tenantId, DateTime from, DateTime to)
    {
        var cacheKey = $"{tenantId}:audit:summary:{from:yyyyMMdd}:{to:yyyyMMdd}";
        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var logs = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.CreatedAt >= from && a.CreatedAt <= to)
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
}
