using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using MURO.Application.DTOs.Auth;
using MURO.Application.Interfaces;

namespace MURO.API.Controllers;

/// <summary>
/// Kimlik doğrulama ve oturum yönetimi.
/// Login, Register, Token yenileme ve Logout işlemlerini yönetir.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthLoginService _loginService;
    private readonly IAuthTokenService _tokenService;
    private readonly IAuthSessionService _sessionService;
    private readonly IBackgroundJobQueue _jobQueue;

    public AuthController(
        IAuthLoginService loginService,
        IAuthTokenService tokenService,
        IAuthSessionService sessionService,
        IBackgroundJobQueue jobQueue)
    {
        _loginService = loginService;
        _tokenService = tokenService;
        _sessionService = sessionService;
        _jobQueue = jobQueue;
    }

    /// <summary>Kullanıcı girişi — JWT token ve refresh token döner.</summary>
    /// <remarks>
    /// Rate limiting uygulanır (5 deneme / 15dk).
    /// Başarılı girişte HttpOnly cookie da set edilir.
    /// </remarks>
    /// <response code="200">Başarılı giriş — JWT + RefreshToken</response>
    /// <response code="401">Geçersiz e-posta veya şifre</response>
    /// <response code="422">Doğrulama hatası (boş alan vb.)</response>
    /// <response code="429">Çok fazla deneme — rate limited</response>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingConfig.AuthPolicy)]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(422)]
    [ProducesResponseType(429)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();
        
        var result = await _loginService.LoginAsync(request, ipAddress, userAgent);
        
        // 🔐 HttpOnly cookie — XSS ile çalınamaz
        CookieAuthMiddleware.SetAuthCookie(HttpContext, result.Token, result.ExpiresAt);

        await _jobQueue.EnqueueAsync(new AuditLogJob(result.User.Tenants?.FirstOrDefault()?.TenantId, result.User.Id, $"{result.User.FirstName} {result.User.LastName}", "Login", "User", result.User.Id.ToString(), $"{result.User.FirstName} {result.User.LastName}", $"Rol: {result.User.Role}", ipAddress));
        
        return Ok(result);
    }

    /// <summary>Yeni kullanıcı kaydı oluşturur.</summary>
    /// <response code="201">Kayıt başarılı — JWT + RefreshToken</response>
    /// <response code="409">E-posta adresi zaten kayıtlı</response>
    /// <response code="422">Doğrulama hatası</response>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitingConfig.AuthPolicy)]
    [ProducesResponseType(typeof(AuthResponse), 201)]
    [ProducesResponseType(409)]
    [ProducesResponseType(422)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var result = await _loginService.RegisterAsync(request);
        CookieAuthMiddleware.SetAuthCookie(HttpContext, result.Token, result.ExpiresAt);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _jobQueue.EnqueueAsync(new AuditLogJob(result.User.Tenants?.FirstOrDefault()?.TenantId, result.User.Id, $"{result.User.FirstName} {result.User.LastName}", "Register", "User", result.User.Id.ToString(), $"{result.User.FirstName} {result.User.LastName}", null, ip));
        return Created("", result);
    }

    /// <summary>Mevcut oturumun kullanıcı bilgilerini getirir.</summary>
    /// <response code="200">Kullanıcı bilgileri</response>
    /// <response code="401">Token geçersiz veya süresi dolmuş</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Invalid token" });

        var user = await _loginService.GetCurrentUserAsync(userId);
        return Ok(user);
    }

    /// <summary>Mevcut oturumu sonlandırır ve cookie temizler.</summary>
    /// <response code="200">Oturum başarıyla kapatıldı</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        await _sessionService.LogoutAsync(userId);
        CookieAuthMiddleware.ClearAuthCookie(HttpContext);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _jobQueue.EnqueueAsync(new AuditLogJob(null, userId, null, "Logout", "User", userId.ToString(), null, null, ip));
        return Ok(new { message = "Oturum kapatıldı" });
    }

    /// <summary>Mevcut refresh token ile yeni JWT access token alır.</summary>
    /// <remarks>Video izlerken token süresi dolduğunda kullanılır.</remarks>
    /// <response code="200">Yeni JWT + RefreshToken</response>
    /// <response code="401">Refresh token geçersiz veya süresi dolmuş</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _tokenService.RefreshTokenAsync(request.RefreshToken);
        CookieAuthMiddleware.SetAuthCookie(HttpContext, result.Token, result.ExpiresAt);
        return Ok(result);
    }
}
