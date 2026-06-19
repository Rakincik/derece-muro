using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MURO.Application.DTOs.Auth;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using StackExchange.Redis;

namespace MURO.Infrastructure.Services;

public class AuthLoginService : AuthServiceBase, IAuthLoginService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public AuthLoginService(MuroDbContext context, IConfiguration config, IConnectionMultiplexer? redis = null)
        : base(context, config, redis)
    {
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null)
    {
        var user = await _context.Users
            .Include(u => u.TenantMemberships).ThenInclude(tm => tm.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user?.LockoutUntil.HasValue == true && user.LockoutUntil > DateTime.UtcNow)
        {
            await LogSecurityEventAsync(user.Id, null, "ACCOUNT_LOCKED", ipAddress, userAgent,
                JsonSerializer.Serialize(new { until = user.LockoutUntil }));
            throw new UnauthorizedAccessException(
                $"Hesabınız geçici olarak kilitlendi. {user.LockoutUntil:HH:mm} sonra tekrar deneyin.");
        }

        bool isPasswordValid = false;
        if (user != null)
        {
            if (user.PasswordHash.StartsWith("$2"))
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            }
            else
            {
                isPasswordValid = (request.Password == user.PasswordHash);
            }
        }

        if (user == null || !isPasswordValid)
        {
            if (user != null)
            {
                user.FailedLoginCount++;
                if (user.FailedLoginCount >= MaxFailedAttempts)
                {
                    user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
                    user.FailedLoginCount = 0;
                    await _context.SaveChangesAsync();

                    await LogSecurityEventAsync(user.Id, null, "BRUTE_FORCE_DETECTED", ipAddress, userAgent,
                        JsonSerializer.Serialize(new { lockoutUntil = user.LockoutUntil }));
                }
                else
                {
                    await _context.SaveChangesAsync();
                    await LogSecurityEventAsync(user.Id, null, "LOGIN_FAILED", ipAddress, userAgent,
                        JsonSerializer.Serialize(new { attempt = user.FailedLoginCount }));
                }
            }
            else
            {
                await LogSecurityEventAsync(null, null, "LOGIN_FAILED", ipAddress, userAgent,
                    JsonSerializer.Serialize(new { email = request.Email }));
            }

            throw new UnauthorizedAccessException("Geçersiz e-posta veya şifre.");
        }

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Hesabınız devre dışı bırakılmış.");

        if (user.StudentType == StudentType.Demo && user.DemoExpiresAt.HasValue && user.DemoExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Demo süreniz dolmuş.");

        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        user.LastLoginAt = DateTime.UtcNow;

        var existingSessions = await _context.DeviceSessions
            .Where(s => s.UserId == user.Id && s.IsActive)
            .ToListAsync();

        var tenantId = user.TenantMemberships.FirstOrDefault(m => m.Status == "active")?.TenantId;

        foreach (var old in existingSessions)
        {
            old.IsActive = false;
            old.LogoutAt = DateTime.UtcNow;
            await LogSecurityEventAsync(user.Id, tenantId, "SESSION_KICKED", ipAddress, userAgent,
                JsonSerializer.Serialize(new
                {
                    kickedSessionId = old.Id,
                    kickedDeviceInfo = old.DeviceInfo,
                    kickedIp = old.IpAddress,
                    newIp = ipAddress
                }));
        }

        var deviceSession = new DeviceSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = ParseDeviceInfo(userAgent),
            LoginAt = DateTime.UtcNow,
            IsActive = true
        };
        _context.DeviceSessions.Add(deviceSession);
        await _context.SaveChangesAsync();

        var lastKnownIp = existingSessions.FirstOrDefault()?.IpAddress;
        if (!string.IsNullOrEmpty(lastKnownIp) && lastKnownIp != ipAddress)
        {
            await LogSecurityEventAsync(user.Id, tenantId, "NEW_IP_LOGIN", ipAddress, userAgent,
                JsonSerializer.Serialize(new { previousIp = lastKnownIp, newIp = ipAddress }));
        }

        await LogSecurityEventAsync(user.Id, tenantId, "LOGIN_SUCCESS", ipAddress, userAgent,
            JsonSerializer.Serialize(new { deviceInfo = deviceSession.DeviceInfo, sessionId = deviceSession.Id }));

        var token = GenerateJwtToken(user, deviceSession.Id);
        var refreshToken = GenerateRefreshToken();

        await StoreRefreshTokenAsync(user.Id, refreshToken, deviceSession.Id);

        return new AuthResponse(token, refreshToken, DateTime.UtcNow.AddHours(AccessTokenExpiryHours), MapToDto(user));
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            throw new InvalidOperationException("Bu e-posta adresi zaten kayıtlı.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = request.Password,
            Role = UserRole.Student,
            StudentType = StudentType.Active,
        };

        _context.Users.Add(user);

        if (request.TenantId.HasValue)
        {
            var tenant = await _context.Tenants.FindAsync(request.TenantId.Value);
            if (tenant != null)
            {
                _context.TenantMemberships.Add(new TenantMembership
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TenantId = tenant.Id,
                    Role = UserRole.Student,
                    Status = "active"
                });
            }
        }

        await _context.SaveChangesAsync();

        user = await _context.Users
            .Include(u => u.TenantMemberships).ThenInclude(tm => tm.Tenant)
            .FirstAsync(u => u.Id == user.Id);

        var deviceSession = new DeviceSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = user.TenantMemberships.FirstOrDefault()?.TenantId,
            LoginAt = DateTime.UtcNow,
            IsActive = true
        };
        _context.DeviceSessions.Add(deviceSession);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user, deviceSession.Id);
        var refreshToken = GenerateRefreshToken();
        await StoreRefreshTokenAsync(user.Id, refreshToken, deviceSession.Id);

        return new AuthResponse(token, refreshToken, DateTime.UtcNow.AddHours(AccessTokenExpiryHours), MapToDto(user));
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.TenantMemberships).ThenInclude(tm => tm.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        return MapToDto(user);
    }
}
