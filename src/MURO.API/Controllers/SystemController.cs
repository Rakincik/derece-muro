using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;
using MURO.Domain.Entities;
using MURO.Application.DTOs.System;
using Microsoft.EntityFrameworkCore;

namespace MURO.API.Controllers;

/// <summary>
/// Sistem sağlık, metrik ve VEP entegrasyon endpoint'leri.
/// VEP Control Plane bu endpoint'leri kullanarak MURO durumunu izler.
/// </summary>
[EnableRateLimiting(RateLimitingConfig.ApiPolicy)]
[ApiController]
[Route("api/v1/system")]
public class SystemController : ControllerBase
{
    private readonly ISystemHealthService _health;
    private readonly IPlatformAnalyticsService _analytics;
    private readonly IVepWebhookService _webhook;
    private readonly MuroDbContext _db;
    private readonly ISecurityService _security;

    public SystemController(ISystemHealthService health, IPlatformAnalyticsService analytics, IVepWebhookService webhook, MuroDbContext db, ISecurityService security)
    {
        _health = health;
        _analytics = analytics;
        _webhook = webhook;
        _db = db;
        _security = security;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 1. DETAYLI SAĞLIK KONTROlÜ
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tüm servislerin detaylı sağlık durumunu döner.
    /// DB, Redis, BBB, Worker, Storage — her birinin response time'ı ile.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous] // Monitoring araçları auth'suz erişebilmeli
    public async Task<IActionResult> GetDetailedHealth()
    {
        var report = await _health.GetHealthReportAsync();

        var statusCode = report.OverallStatus switch
        {
            "healthy" => 200,
            "degraded" => 200, // Degraded hâlâ çalışıyor — 200
            _ => 503           // Unhealthy → 503
        };

        return StatusCode(statusCode, report);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. PLATFORM İSTATİSTİKLERİ
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Platform istatistikleri — öğrenci, kurs, oturum, kayıt sayıları.
    /// VEP dashboard bu veriyi gösterir.
    /// </summary>
    [HttpGet("stats")]
    [AllowAnonymous] // VEP Agent auth'suz çağırabilmeli (ya da API key ile)
    public async Task<IActionResult> GetPlatformStats()
    {
        var stats = await _analytics.GetPlatformStatsAsync();
        return Ok(stats);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. VEP WEBHOOK DURUMU
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// VEP webhook bağlantı durumunu kontrol eder.
    /// </summary>
    [HttpGet("vep/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetVepStatus()
    {
        var configured = await _webhook.IsConfiguredAsync();
        return Ok(new
        {
            configured,
            message = configured
                ? "VEP webhook aktif — olaylar bildirilecek."
                : "VEP webhook URL yapılandırılmamış. appsettings.json → Vep:WebhookUrl"
        });
    }

    /// <summary>
    /// VEP webhook'a test bildirimi gönderir.
    /// </summary>
    [HttpPost("vep/test")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TestVepWebhook()
    {
        var configured = await _webhook.IsConfiguredAsync();
        if (!configured)
            return BadRequest(new { error = "VEP webhook URL yapılandırılmamış." });

        await _webhook.NotifyAsync(new VepWebhookEvent
        {
            EventType = "system.test",
            TenantCode = "system",
            Data = new Dictionary<string, object>
            {
                ["message"] = "MURO → VEP bağlantı testi başarılı!",
                ["sentAt"] = DateTime.UtcNow.ToString("o"),
            }
        });

        return Ok(new { success = true, message = "Test webhook gönderildi." });
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4. VEP PROVISIONING (KURUM & ADMIN OLUŞTURMA)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// VEP üzerinden gelen yeni Kurum ve Yönetici oluşturma (Provisioning) isteğini işler.
    /// VEP bu isteği güvenli bir iç kanaldan (veya API key ile) gönderir.
    /// </summary>
    [HttpPost("provision")]
    [AllowAnonymous] // TODO: API Key / Secret eklenecek
    public async Task<IActionResult> ProvisionTenant([FromBody] ProvisionTenantRequest req)
    {
        // 1. Kurumun daha önce olup olmadığını kontrol et
        var exists = await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == req.TenantId || t.Code == req.Slug);
        if (exists)
            return BadRequest(new { error = "Bu ID veya Slug'a sahip bir kurum zaten mevcut." });

        // 2. Yeni Kurumu Oluştur
        var tenant = new Tenant
        {
            Id = req.TenantId,
            Name = req.Name,
            Code = req.Slug,
            Subdomain = req.Subdomain ?? req.Slug,
            Domain = req.Domain,
            LogoUrl = req.LogoUrl,
            FaviconUrl = req.FaviconUrl,
            PrimaryColor = req.PrimaryColor,
            AccentColor = req.AccentColor,
            FooterText = req.FooterText,
            Features = req.Features,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tenants.Add(tenant);

        // 3. İlk Yöneticiyi (Admin) Oluştur
        // Email var mı kontrolü (Sistem genelinde email eşsiz değil, tenant bazlı eşsiz ama biz adminleri ayırıyoruz)
        var userExists = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == req.AdminEmail);
        User adminUser;
        if (userExists)
        {
            adminUser = await _db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == req.AdminEmail);
            // Zaten varsa sadece yeni kuruma yetki verelim. (Multi-tenant user)
        }
        else
        {
            adminUser = new User
            {
                Id = Guid.NewGuid(),
                FirstName = req.AdminFirstName,
                LastName = req.AdminLastName,
                Email = req.AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.AdminPassword),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Role = MURO.Domain.Enums.UserRole.Admin
            };
            _db.Users.Add(adminUser);
        }

        // 4. Admin Yetkisi Ata (TenantMembership)
        var membership = new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = adminUser.Id,
            Role = MURO.Domain.Enums.UserRole.Admin, // Kurum yetkilisi "Admin"
            Status = "active",
            JoinedAt = DateTime.UtcNow
        };

        _db.TenantMemberships.Add(membership);

        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Kurum ve yönetici başarıyla oluşturuldu.", tenantId = tenant.Id });
    }
}
