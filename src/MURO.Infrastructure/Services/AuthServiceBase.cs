using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MURO.Application.DTOs.Auth;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;
using StackExchange.Redis;

namespace MURO.Infrastructure.Services;

public abstract class AuthServiceBase
{
    protected readonly MuroDbContext _context;
    protected readonly IConfiguration _config;
    protected readonly IConnectionMultiplexer? _redis;

    protected int AccessTokenExpiryHours => _config.GetValue("Jwt:AccessTokenExpiryHours", 2);
    protected int RefreshTokenExpiryDays => _config.GetValue("Jwt:RefreshTokenExpiryDays", 7);

    protected AuthServiceBase(MuroDbContext context, IConfiguration config, IConnectionMultiplexer? redis = null)
    {
        _context = context;
        _config = config;
        _redis = redis;
    }

    protected async Task LogSecurityEventAsync(
        Guid? userId, Guid? tenantId, string eventType,
        string? ip, string? userAgent, string? details = null)
    {
        _context.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId,
            TenantId = tenantId,
            EventType = eventType,
            IpAddress = ip,
            UserAgent = userAgent,
            Details = details
        });
        await _context.SaveChangesAsync();
    }

    protected string GenerateJwtToken(User user, Guid sessionId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured.")));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("firstName", user.FirstName),
            new("lastName", user.LastName),
            new("sessionId", sessionId.ToString()),
        };

        foreach (var membership in user.TenantMemberships.Where(m => m.Status == "active"))
            claims.Add(new Claim("tenantId", membership.TenantId.ToString()));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "MURO",
            audience: _config["Jwt:Audience"] ?? "MURO",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(AccessTokenExpiryHours),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    protected async Task StoreRefreshTokenAsync(Guid userId, string refreshToken, Guid sessionId)
    {
        var db = _redis?.GetDatabase();
        if (db == null) return;

        var data = JsonSerializer.Serialize(new RefreshTokenData(userId, sessionId));
        await db.StringSetAsync(
            $"refresh:{refreshToken}",
            data,
            TimeSpan.FromDays(RefreshTokenExpiryDays)
        );
    }

    public record RefreshTokenData(Guid UserId, Guid SessionId);

    protected static string ParseDeviceInfo(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return "Bilinmeyen Cihaz";
        if (userAgent.Contains("iPhone")) return "iPhone";
        if (userAgent.Contains("Android")) return "Android Cihaz";
        if (userAgent.Contains("Windows")) return "Windows PC";
        if (userAgent.Contains("Macintosh")) return "Mac";
        return "Web Tarayıcı";
    }

    protected static UserDto MapToDto(User user) => new(
        user.Id, user.FirstName, user.LastName, user.Email, user.Phone,
        user.Role.ToString(), user.StudentType?.ToString(), user.DemoExpiresAt,
        user.IsActive, user.CreatedAt,
        user.TenantMemberships.Select(tm => new UserTenantDto(
            tm.TenantId, tm.Tenant.Name, tm.Tenant.Code, tm.Role.ToString(), tm.Status, tm.Tenant.Features
        )).ToList()
    );
}
