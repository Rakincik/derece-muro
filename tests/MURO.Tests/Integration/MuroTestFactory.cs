using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Tests.Integration;

/// <summary>
/// Integration test altyapısı — gerçek HTTP pipeline'ı test eder.
/// InMemory DB + JWT token üretimi + Tenant header.
/// </summary>
public class MuroTestFactory : WebApplicationFactory<Program>
{
    public const string JwtSecret = "MuroTestSuperSecretKey_AtLeast32Chars!";
    public const string JwtIssuer = "MURO_TEST";
    public const string JwtAudience = "MURO_TEST";

    public Guid TestTenantId { get; } = Guid.NewGuid();
    public Guid TestAdminId { get; } = Guid.NewGuid();
    public Guid TestStudentId { get; } = Guid.NewGuid();
    public Guid TestAdminSessionId { get; } = Guid.NewGuid();
    public Guid TestStudentSessionId { get; } = Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Gerçek DB registrations'ları temizle
            var dbDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<MuroDbContext>)
                         || d.ServiceType == typeof(MuroDbContext))
                .ToList();
            foreach (var d in dbDescriptors) services.Remove(d);

            services.AddDbContext<MuroDbContext>(options =>
                options.UseInMemoryDatabase("MuroIntegrationTests_Shared"));
        });

        builder.UseSetting("Jwt:Secret", JwtSecret);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
    }

    /// <summary>Test DB'ye seed data yükler (idempotent — birden fazla çağrıda güvenli)</summary>
    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MuroDbContext>();

        // Zaten seed edilmişse atla
        if (await db.Tenants.AnyAsync(t => t.Id == TestTenantId))
            return;

        db.Tenants.Add(new Tenant
        {
            Id = TestTenantId, Name = "Test Üniversitesi", Code = "test",
            IsActive = true, CreatedAt = DateTime.UtcNow
        });

        var adminUser = new User
        {
            Id = TestAdminId, Email = "admin@test.com",
            FirstName = "Admin", LastName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@1234"),
            Role = UserRole.Admin, IsActive = true
        };
        var studentUser = new User
        {
            Id = TestStudentId, Email = "student@test.com",
            FirstName = "Student", LastName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Student@1234"),
            Role = UserRole.Student, IsActive = true
        };
        db.Users.AddRange(adminUser, studentUser);

        db.TenantMemberships.AddRange(
            new TenantMembership { Id = Guid.NewGuid(), UserId = TestAdminId, TenantId = TestTenantId, Role = UserRole.Admin, Status = "active" },
            new TenantMembership { Id = Guid.NewGuid(), UserId = TestStudentId, TenantId = TestTenantId, Role = UserRole.Student, Status = "active" }
        );

        // Device sessions (SessionValidationMiddleware için)
        db.DeviceSessions.AddRange(
            new DeviceSession { Id = TestAdminSessionId, UserId = TestAdminId, IsActive = true, CreatedAt = DateTime.UtcNow, IpAddress = "127.0.0.1" },
            new DeviceSession { Id = TestStudentSessionId, UserId = TestStudentId, IsActive = true, CreatedAt = DateTime.UtcNow, IpAddress = "127.0.0.1" }
        );

        await db.SaveChangesAsync();
    }

    /// <summary>Belirli rol ve kullanıcı için JWT token üretir</summary>
    public string GenerateToken(Guid userId, string role, Guid sessionId, Guid tenantId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim("tenantId", tenantId.ToString()),
            new Claim("sessionId", sessionId.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer, audience: JwtAudience,
            claims: claims, expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Admin token'lı HTTP client</summary>
    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        var token = GenerateToken(TestAdminId, "Admin", TestAdminSessionId, TestTenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId.ToString());
        return client;
    }

    /// <summary>Student token'lı HTTP client</summary>
    public HttpClient CreateStudentClient()
    {
        var client = CreateClient();
        var token = GenerateToken(TestStudentId, "Student", TestStudentSessionId, TestTenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId.ToString());
        return client;
    }

    /// <summary>Token'sız anonim HTTP client</summary>
    public HttpClient CreateAnonymousClient()
    {
        return CreateClient();
    }
}
