using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MURO.Application.DTOs.Auth;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;
using StackExchange.Redis;

namespace MURO.Infrastructure.Services;

public class AuthSessionService : AuthServiceBase, IAuthSessionService
{
    public AuthSessionService(MuroDbContext context, IConfiguration config, IConnectionMultiplexer? redis = null)
        : base(context, config, redis)
    {
    }

    public async Task LogoutAsync(Guid userId, string? refreshToken = null)
    {
        var activeSessions = await _context.DeviceSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.LogoutAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task RevokeSessionAsync(Guid userId, Guid sessionId)
    {
        var session = await _context.DeviceSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Id == sessionId);

        if (session != null)
        {
            session.IsActive = false;
            session.LogoutAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<DeviceSessionDto>> GetActiveSessionsAsync(Guid userId)
    {
        var sessions = await _context.DeviceSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.LoginAt)
            .Select(s => new DeviceSessionDto(
                s.Id,
                s.DeviceInfo,
                s.IpAddress,
                s.LoginAt,
                s.UserAgent,
                false
            ))
            .ToListAsync();

        return sessions;
    }

    public async Task UpdatePushTokenAsync(Guid userId, string pushToken, string? ipAddress, string? userAgent)
    {
        // Try to find the exact session by IP and UserAgent (most recent active)
        var session = await _context.DeviceSessions
            .Where(s => s.UserId == userId && s.IsActive && s.IpAddress == ipAddress && s.UserAgent == userAgent)
            .OrderByDescending(s => s.LoginAt)
            .FirstOrDefaultAsync();

        // Fallback: If not found, just get the latest active session for this user
        session ??= await _context.DeviceSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.LoginAt)
            .FirstOrDefaultAsync();

        if (session != null)
        {
            session.PushToken = pushToken;
            await _context.SaveChangesAsync();
        }
    }
}
