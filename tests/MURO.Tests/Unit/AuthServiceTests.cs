using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MURO.Application.DTOs.Auth;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// AuthService için unit testler.
/// EF Core InMemory kullanılır — gerçek DB bağlantısı gerekmez.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly AuthLoginService _authService;
    private readonly IConfiguration _config;

    private const string ValidPassword = "Test@1234";
    private const string WrongPassword  = "WrongPass!";

    public AuthServiceTests()
    {
        // Her test için izole InMemory DB
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"]   = "MuroTestSuperSecretKey_AtLeast32Chars!",
                ["Jwt:Issuer"]   = "MURO_TEST",
                ["Jwt:Audience"] = "MURO_TEST"
            })
            .Build();

        _authService = new AuthLoginService(_db, _config);
    }

    // ── Yardımcı: Test kullanıcısı oluştur ──────────────────────────────────
    private async Task<User> CreateUserAsync(string email = "test@muro.com", bool isActive = true)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(), Name = "Test Tenant", Code = "test",
            CreatedAt = DateTime.UtcNow
        };
        _db.Tenants.Add(tenant);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = "Test",
            LastName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(ValidPassword),
            Role = UserRole.Student,
            IsActive = isActive
        };
        _db.Users.Add(user);
        _db.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(), UserId = user.Id,
            TenantId = tenant.Id, Role = UserRole.Student, Status = "active"
        });

        await _db.SaveChangesAsync();
        return user;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Login — Başarılı Senaryolar
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ShouldReturnToken()
    {
        // Arrange
        await CreateUserAsync();

        // Act
        var result = await _authService.LoginAsync(
            new LoginRequest("test@muro.com", ValidPassword));

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
        result.User.Email.Should().Be("test@muro.com");
    }

    [Fact]
    public async Task Login_ValidCredentials_ShouldCreateDeviceSession()
    {
        await CreateUserAsync();

        await _authService.LoginAsync(new LoginRequest("test@muro.com", ValidPassword));

        var sessions = await _db.DeviceSessions.Where(s => s.IsActive).ToListAsync();
        sessions.Should().HaveCount(1);
    }

    [Fact]
    public async Task Login_ValidCredentials_ShouldLogLoginSuccess()
    {
        await CreateUserAsync();

        await _authService.LoginAsync(new LoginRequest("test@muro.com", ValidPassword));

        var events = await _db.SecurityEvents.ToListAsync();
        events.Should().Contain(e => e.EventType == "LOGIN_SUCCESS");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Login — Başarısız Senaryolar
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WrongPassword_ShouldThrowUnauthorized()
    {
        await CreateUserAsync();

        Func<Task> act = () => _authService.LoginAsync(
            new LoginRequest("test@muro.com", WrongPassword));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Geçersiz*");
    }

    [Fact]
    public async Task Login_WrongPassword_ShouldIncrementFailedCount()
    {
        var user = await CreateUserAsync();

        try { await _authService.LoginAsync(new LoginRequest(user.Email, WrongPassword)); } catch { }

        var updatedUser = await _db.Users.FindAsync(user.Id);
        updatedUser!.FailedLoginCount.Should().Be(1);
    }

    [Fact]
    public async Task Login_WrongPassword_ShouldLogLoginFailed()
    {
        await CreateUserAsync();

        try { await _authService.LoginAsync(new LoginRequest("test@muro.com", WrongPassword)); } catch { }

        var events = await _db.SecurityEvents.ToListAsync();
        events.Should().Contain(e => e.EventType == "LOGIN_FAILED");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Brute-Force Koruma
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_5WrongPasswords_ShouldLockAccount()
    {
        var user = await CreateUserAsync();

        // 5 kez yanlış şifre
        for (int i = 0; i < 5; i++)
        {
            try { await _authService.LoginAsync(new LoginRequest(user.Email, WrongPassword)); } catch { }
        }

        var updatedUser = await _db.Users.FindAsync(user.Id);
        updatedUser!.LockoutUntil.Should().NotBeNull();
        updatedUser.LockoutUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_5WrongPasswords_ShouldLogBruteForce()
    {
        var user = await CreateUserAsync();

        for (int i = 0; i < 5; i++)
        {
            try { await _authService.LoginAsync(new LoginRequest(user.Email, WrongPassword)); } catch { }
        }

        var events = await _db.SecurityEvents.ToListAsync();
        events.Should().Contain(e => e.EventType == "BRUTE_FORCE_DETECTED");
    }

    [Fact]
    public async Task Login_LockedAccount_ShouldThrowWithLockMessage()
    {
        var user = await CreateUserAsync();

        // Hesabı manuel olarak kilitle
        user.LockoutUntil = DateTime.UtcNow.AddMinutes(10);
        await _db.SaveChangesAsync();

        Func<Task> act = () => _authService.LoginAsync(
            new LoginRequest(user.Email, ValidPassword));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*kilitlendi*");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Tek Cihaz Politikası — Session Kickout
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_SecondDevice_ShouldDeactivatePreviousSession()
    {
        await CreateUserAsync();

        // İlk giriş (Cihaz A)
        await _authService.LoginAsync(
            new LoginRequest("test@muro.com", ValidPassword), "1.1.1.1", "Chrome/Win");

        // İkinci giriş (Cihaz B)
        await _authService.LoginAsync(
            new LoginRequest("test@muro.com", ValidPassword), "2.2.2.2", "Firefox/Mac");

        // Eski oturum kapalı, yeni oturum açık
        var activeSessions = await _db.DeviceSessions.Where(s => s.IsActive).ToListAsync();
        var allSessions    = await _db.DeviceSessions.ToListAsync();

        allSessions.Should().HaveCount(2);   // ikisi de kayıtlı
        activeSessions.Should().HaveCount(1); // sadece biri aktif
        activeSessions[0].IpAddress.Should().Be("2.2.2.2"); // yeni cihaz
    }

    [Fact]
    public async Task Login_SecondDevice_ShouldLogSessionKicked()
    {
        await CreateUserAsync();

        await _authService.LoginAsync(new LoginRequest("test@muro.com", ValidPassword), "1.1.1.1");
        await _authService.LoginAsync(new LoginRequest("test@muro.com", ValidPassword), "2.2.2.2");

        var events = await _db.SecurityEvents.ToListAsync();
        events.Should().Contain(e => e.EventType == "SESSION_KICKED");
    }

    [Fact]
    public async Task Login_DifferentIp_ShouldLogNewIpLogin()
    {
        await CreateUserAsync();

        // İlk giriş farklı IP'den session oluşturur, sonra deactivate edilir
        await _authService.LoginAsync(new LoginRequest("test@muro.com", ValidPassword), "1.1.1.1");
        var events1 = await _db.SecurityEvents.ToListAsync();
        _db.SecurityEvents.RemoveRange(events1); // temizle
        await _db.SaveChangesAsync();

        // İkinci giriş farklı IP'den
        await _authService.LoginAsync(new LoginRequest("test@muro.com", ValidPassword), "9.9.9.9");

        var events = await _db.SecurityEvents.ToListAsync();
        events.Should().Contain(e => e.EventType == "NEW_IP_LOGIN");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Pasif Kullanıcı
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_InactiveUser_ShouldThrow()
    {
        await CreateUserAsync(isActive: false);

        Func<Task> act = () => _authService.LoginAsync(
            new LoginRequest("test@muro.com", ValidPassword));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*devre dışı*");
    }

    public void Dispose() => _db.Dispose();
}
