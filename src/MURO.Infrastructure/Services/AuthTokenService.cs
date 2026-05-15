using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MURO.Application.DTOs.Auth;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;
using StackExchange.Redis;

namespace MURO.Infrastructure.Services;

public class AuthTokenService : AuthServiceBase, IAuthTokenService
{
    public AuthTokenService(MuroDbContext context, IConfiguration config, IConnectionMultiplexer? redis = null)
        : base(context, config, redis)
    {
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        var redisKey = $"refresh:{refreshToken}";
        var db = _redis?.GetDatabase();
        if (db == null)
            throw new InvalidOperationException("Redis bağlantısı gerekli.");

        var data = await db.StringGetAsync(redisKey);
        if (data.IsNullOrEmpty)
            throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş refresh token.");

        var tokenData = JsonSerializer.Deserialize<RefreshTokenData>(data!);
        if (tokenData == null)
            throw new UnauthorizedAccessException("Geçersiz refresh token verisi.");

        var user = await _context.Users
            .Include(u => u.TenantMemberships).ThenInclude(tm => tm.Tenant)
            .FirstOrDefaultAsync(u => u.Id == tokenData.UserId)
            ?? throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

        var session = await _context.DeviceSessions
            .FirstOrDefaultAsync(s => s.Id == tokenData.SessionId && s.IsActive);
        if (session == null)
            throw new UnauthorizedAccessException("Oturum sonlandırılmış.");

        await db.KeyDeleteAsync(redisKey);

        var newAccessToken = GenerateJwtToken(user, session.Id);
        var newRefreshToken = GenerateRefreshToken();
        await StoreRefreshTokenAsync(user.Id, newRefreshToken, session.Id);

        return new AuthResponse(newAccessToken, newRefreshToken, DateTime.UtcNow.AddHours(AccessTokenExpiryHours), MapToDto(user));
    }
}
