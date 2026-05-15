using MURO.Application.DTOs.Auth;

namespace MURO.Application.Interfaces;

public interface IAuthTokenService
{
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
}
