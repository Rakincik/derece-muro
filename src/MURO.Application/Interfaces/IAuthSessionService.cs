using MURO.Application.DTOs.Auth;

namespace MURO.Application.Interfaces;

public interface IAuthSessionService
{
    Task LogoutAsync(Guid userId, string? refreshToken = null);
    Task RevokeSessionAsync(Guid userId, Guid sessionId);
    Task<List<DeviceSessionDto>> GetActiveSessionsAsync(Guid userId);
    Task UpdatePushTokenAsync(Guid userId, string pushToken, string? ipAddress, string? userAgent);
}
