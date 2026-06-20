namespace MURO.Application.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? Phone
);

public record AuthResponse(
    string Token,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record RefreshTokenRequest(string RefreshToken);

public record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string? Phone,
    string Role,
    string? StudentType,
    DateTime? DemoExpiresAt,
    bool IsActive,
    DateTime CreatedAt,
    List<UserTenantDto> Tenants,
    string? TcNo = null
);

public record UserTenantDto(
    string TenantId,
    string TenantName,
    string TenantCode,
    string Role,
    string Status,
    string? Features
);

public record DeviceSessionDto(
    Guid Id,
    string DeviceInfo,
    string? IpAddress,
    DateTime LoginAt,
    string? UserAgent,
    bool IsCurrent
);
