using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Security;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class SecurityService : ISecurityService
{
    private readonly MuroDbContext _context;

    public SecurityService(MuroDbContext context)
    {
        _context = context;
    }

    public async Task<SecurityEventPageDto> GetEventsAsync(Guid tenantId, DateTime? from, DateTime? to, Guid? userId, string? eventType, int page, int pageSize)
    {
        var query = _context.SecurityEvents
            .AsNoTracking()
            .Where(se => se.TenantId == tenantId || se.UserId != null && _context.TenantMemberships
                .Any(tm => tm.UserId == se.UserId && tm.TenantId == tenantId));

        if (from.HasValue) query = query.Where(se => se.CreatedAt >= from.Value);
        if (to.HasValue)   query = query.Where(se => se.CreatedAt <= to.Value);
        if (userId.HasValue) query = query.Where(se => se.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(eventType)) query = query.Where(se => se.EventType == eventType);

        var total = await query.CountAsync();

        var events = await query
            .OrderByDescending(se => se.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(se => new SecurityEventDto(
                se.Id, se.UserId,
                se.User != null ? se.User.FirstName + " " + se.User.LastName : null,
                se.EventType, se.IpAddress, se.UserAgent, se.Details, se.CreatedAt))
            .ToListAsync();

        return new SecurityEventPageDto(events, total, page, pageSize);
    }

    public async Task<List<SecuritySummaryDto>> GetSummaryAsync(Guid tenantId)
    {
        var since = DateTime.UtcNow.AddHours(-24);

        var summary = await _context.SecurityEvents
            .AsNoTracking()
            .Where(se => se.CreatedAt >= since &&
                (se.TenantId == tenantId ||
                 se.UserId != null && _context.TenantMemberships
                     .Any(tm => tm.UserId == se.UserId && tm.TenantId == tenantId)))
            .GroupBy(se => se.EventType)
            .Select(g => new SecuritySummaryDto(g.Key, g.Count()))
            .ToListAsync();

        return summary;
    }

    public async Task<List<SecurityEventDto>> GetSuspiciousActivityAsync(Guid tenantId)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var suspiciousTypes = new[] { "SESSION_KICKED", "BRUTE_FORCE_DETECTED", "NEW_IP_LOGIN" };

        var suspicious = await _context.SecurityEvents
            .AsNoTracking()
            .Where(se => se.CreatedAt >= since
                && suspiciousTypes.Contains(se.EventType)
                && se.UserId != null
                && _context.TenantMemberships.Any(tm => tm.UserId == se.UserId && tm.TenantId == tenantId))
            .Include(se => se.User)
            .OrderByDescending(se => se.CreatedAt)
            .Select(se => new SecurityEventDto(
                se.Id, se.UserId,
                se.User != null ? se.User.FirstName + " " + se.User.LastName : null,
                se.EventType, se.IpAddress, se.UserAgent, se.Details, se.CreatedAt))
            .ToListAsync();

        return suspicious;
    }
}
