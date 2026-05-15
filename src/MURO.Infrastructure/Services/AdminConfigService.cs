using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class AdminConfigService : IAdminConfigService
{
    private readonly MuroDbContext _db;
    private readonly IConfiguration _config;

    public AdminConfigService(MuroDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    private (int, object?) Ok(object? data = null) => (200, data);

    public async Task<(int, object?)> GetStorageStats()
    {
        var tenantStorage = await _db.MediaAssets
            .GroupBy(a => a.TenantId)
            .Select(g => new
            {
                tenantId = g.Key,
                totalAssets = g.Count(),
                readyAssets = g.Count(a => a.Status == MediaStatus.Ready),
                processingAssets = g.Count(a => a.Status == MediaStatus.Processing),
                failedAssets = g.Count(a => a.Status == MediaStatus.Failed),
            })
            .ToListAsync();

        var tenants = await _db.Tenants.AsNoTracking()
            .Select(t => new { t.Id, t.Name, t.Code, t.StorageLimitGb })
            .ToDictionaryAsync(t => t.Id);

        var recordingCounts = await _db.SessionRecordings
            .GroupBy(r => r.Session.Course.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count);

        var items = tenantStorage.Select(ts =>
        {
            tenants.TryGetValue(ts.tenantId, out var tenant);
            recordingCounts.TryGetValue(ts.tenantId, out var recCount);
            return new
            {
                tenantId = ts.tenantId,
                tenantName = tenant?.Name ?? "?",
                tenantCode = tenant?.Code ?? "?",
                storageLimitGb = tenant?.StorageLimitGb,
                totalAssets = ts.totalAssets,
                readyAssets = ts.readyAssets,
                processingAssets = ts.processingAssets,
                failedAssets = ts.failedAssets,
                recordings = recCount,
            };
        }).OrderByDescending(x => x.totalAssets).ToList();

        return Ok(new
        {
            items,
            totals = new
            {
                totalAssets = tenantStorage.Sum(t => t.totalAssets),
                totalRecordings = recordingCounts.Values.Sum(),
                totalTenants = items.Count,
            },
        });
    }

    public async Task<(int, object?)> GetSecrets()
    {
        var platformSecrets = new List<object>
        {
            new { key = "JWT Secret", category = "auth", value = MaskSecret(_config["Jwt:Secret"]), configured = !string.IsNullOrEmpty(_config["Jwt:Secret"]) },
            new { key = "JWT Issuer", category = "auth", value = _config["Jwt:Issuer"] ?? "not set", configured = !string.IsNullOrEmpty(_config["Jwt:Issuer"]) },
            new { key = "Access Token Süresi", category = "auth", value = $"{_config["Jwt:AccessTokenExpiryHours"] ?? "?"} saat", configured = !string.IsNullOrEmpty(_config["Jwt:AccessTokenExpiryHours"]) },
            new { key = "Refresh Token Süresi", category = "auth", value = $"{_config["Jwt:RefreshTokenExpiryDays"] ?? "?"} gün", configured = !string.IsNullOrEmpty(_config["Jwt:RefreshTokenExpiryDays"]) },
            new { key = "DB Connection String", category = "database", value = MaskConnectionString(_config.GetConnectionString("DefaultConnection") ?? ""), configured = !string.IsNullOrEmpty(_config.GetConnectionString("DefaultConnection")) },
            new { key = "Redis Connection", category = "cache", value = MaskSecret(_config.GetConnectionString("Redis")), configured = !string.IsNullOrEmpty(_config.GetConnectionString("Redis")) },
            new { key = "BBB Server URL", category = "bbb", value = _config["Bbb:Url"] ?? "not set", configured = !string.IsNullOrEmpty(_config["Bbb:Url"]) },
            new { key = "BBB Secret", category = "bbb", value = MaskSecret(_config["Bbb:Secret"]), configured = !string.IsNullOrEmpty(_config["Bbb:Secret"]) },
            new { key = "BBB Webhook Secret", category = "bbb", value = MaskSecret(_config["Bbb:WebhookSharedSecret"]), configured = !string.IsNullOrEmpty(_config["Bbb:WebhookSharedSecret"]) },
            new { key = "BBB Default Attendee Pw", category = "bbb", value = MaskSecret(_config["Bbb:DefaultAttendeePw"]), configured = !string.IsNullOrEmpty(_config["Bbb:DefaultAttendeePw"]) },
            new { key = "BBB Default Moderator Pw", category = "bbb", value = MaskSecret(_config["Bbb:DefaultModeratorPw"]), configured = !string.IsNullOrEmpty(_config["Bbb:DefaultModeratorPw"]) },
            new { key = "Gemini API Key", category = "ai", value = MaskSecret(_config["Gemini:ApiKey"]), configured = !string.IsNullOrEmpty(_config["Gemini:ApiKey"]) && _config["Gemini:ApiKey"] != "YOUR_GEMINI_API_KEY_HERE" },
            new { key = "EdgeTTS Base URL", category = "ai", value = _config["EdgeTts:BaseUrl"] ?? "not set", configured = !string.IsNullOrEmpty(_config["EdgeTts:BaseUrl"]) },
            new { key = "SMTP Host", category = "email", value = _config["Notifications:Email:SmtpHost"] ?? "not set", configured = !string.IsNullOrEmpty(_config["Notifications:Email:SmtpHost"]) },
            new { key = "SMTP Username", category = "email", value = MaskSecret(_config["Notifications:Email:Username"]), configured = !string.IsNullOrEmpty(_config["Notifications:Email:Username"]) },
            new { key = "SMTP Password", category = "email", value = MaskSecret(_config["Notifications:Email:Password"]), configured = !string.IsNullOrEmpty(_config["Notifications:Email:Password"]) },
            new { key = "SMS Provider", category = "sms", value = _config["Notifications:Sms:Provider"] ?? "not set", configured = !string.IsNullOrEmpty(_config["Notifications:Sms:Provider"]) },
            new { key = "SMS Username", category = "sms", value = MaskSecret(_config["Notifications:Sms:Username"]), configured = !string.IsNullOrEmpty(_config["Notifications:Sms:Username"]) },
            new { key = "SMS Password", category = "sms", value = MaskSecret(_config["Notifications:Sms:Password"]), configured = !string.IsNullOrEmpty(_config["Notifications:Sms:Password"]) },
            new { key = "BunnyCDN API Key", category = "storage", value = MaskSecret(_config["BunnyCdn:ApiKey"]), configured = !string.IsNullOrEmpty(_config["BunnyCdn:ApiKey"]) },
            new { key = "Webhook Secret", category = "integration", value = MaskSecret(_config["Webhook:Secret"]), configured = !string.IsNullOrEmpty(_config["Webhook:Secret"]) },
            new { key = "VEP Webhook URL", category = "integration", value = _config["Vep:WebhookUrl"] ?? "not set", configured = !string.IsNullOrEmpty(_config["Vep:WebhookUrl"]) },
            new { key = "VEP Webhook Secret", category = "integration", value = MaskSecret(_config["Vep:WebhookSecret"]), configured = !string.IsNullOrEmpty(_config["Vep:WebhookSecret"]) },
        };

        var tenantSecrets = await _db.Tenants
            .AsNoTracking()
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Code,
                bbbUrl = t.BbbServerUrl,
                bbbSecretConfigured = t.BbbSecret != null && t.BbbSecret.Length > 0,
                bbbSecretHint = t.BbbSecret != null && t.BbbSecret.Length > 8
                    ? t.BbbSecret.Substring(0, 4) + "..." + t.BbbSecret.Substring(t.BbbSecret.Length - 4)
                    : t.BbbSecret != null ? "***" : null,
                connectionStringConfigured = t.ConnectionString != null && t.ConnectionString.Length > 0,
            })
            .ToListAsync();

        return (200, new { platformSecrets, tenantSecrets });
    }

    public (int, object?) GetGeneralSettings()
    {
        return Ok(new
        {
            auth = new
            {
                jwtIssuer = _config["Jwt:Issuer"],
                jwtAudience = _config["Jwt:Audience"],
                accessTokenExpiryHours = _config["Jwt:AccessTokenExpiryHours"],
                refreshTokenExpiryDays = _config["Jwt:RefreshTokenExpiryDays"],
            },
            tenant = new
            {
                baseDomain = _config["Tenant:BaseDomain"],
            },
            logging = new
            {
                minimumLevel = _config["Serilog:MinimumLevel:Default"] ?? _config["Logging:LogLevel:Default"],
                aspnetLevel = _config["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] ?? _config["Logging:LogLevel:Microsoft.AspNetCore"],
            },
            allowedHosts = _config["AllowedHosts"],
            bbbDefaults = new
            {
                logoutUrl = _config["Bbb:Defaults:LogoutURL"],
            },
        });
    }

    public (int, object?) GetIntegrationStatus()
    {
        var integrations = new List<object>();

        var emailEnabled = _config.GetValue<bool>("Notifications:Email:Enabled");
        var smtpHost = _config["Notifications:Email:SmtpHost"] ?? "";
        integrations.Add(new
        {
            name = "Email (SMTP)",
            category = "email",
            enabled = emailEnabled,
            configured = !string.IsNullOrEmpty(_config["Notifications:Email:Username"]),
            config = new { host = smtpHost, port = _config["Notifications:Email:SmtpPort"], from = _config["Notifications:Email:FromEmail"] },
            status = emailEnabled && !string.IsNullOrEmpty(_config["Notifications:Email:Username"]) ? "active" : emailEnabled ? "misconfigured" : "disabled",
        });

        var smsEnabled = _config.GetValue<bool>("Notifications:Sms:Enabled");
        integrations.Add(new
        {
            name = "SMS (NetGSM)",
            category = "sms",
            enabled = smsEnabled,
            configured = !string.IsNullOrEmpty(_config["Notifications:Sms:Username"]),
            config = new { provider = _config["Notifications:Sms:Provider"], header = _config["Notifications:Sms:Header"] },
            status = smsEnabled && !string.IsNullOrEmpty(_config["Notifications:Sms:Username"]) ? "active" : smsEnabled ? "misconfigured" : "disabled",
        });

        var bbbUrl = _config["Bbb:Url"] ?? "";
        var bbbConfigured = !string.IsNullOrEmpty(bbbUrl) && bbbUrl != "https://";
        integrations.Add(new
        {
            name = "BigBlueButton",
            category = "bbb",
            enabled = bbbConfigured,
            configured = bbbConfigured && !string.IsNullOrEmpty(_config["Bbb:Secret"]),
            config = new { url = bbbUrl },
            status = bbbConfigured ? "active" : "disabled",
        });

        var redisConn = _config.GetConnectionString("Redis") ?? "";
        integrations.Add(new
        {
            name = "Redis Cache",
            category = "cache",
            enabled = !string.IsNullOrEmpty(redisConn),
            configured = !string.IsNullOrEmpty(redisConn),
            config = new { connection = redisConn },
            status = !string.IsNullOrEmpty(redisConn) ? "active" : "disabled",
        });

        var geminiKey = _config["Gemini:ApiKey"] ?? "";
        integrations.Add(new
        {
            name = "Gemini AI",
            category = "ai",
            enabled = !string.IsNullOrEmpty(geminiKey) && !geminiKey.Contains("YOUR_"),
            configured = !string.IsNullOrEmpty(geminiKey) && !geminiKey.Contains("YOUR_"),
            config = new { keyHint = geminiKey.Length > 8 ? geminiKey[..4] + "..." : "not set" },
            status = !string.IsNullOrEmpty(geminiKey) && !geminiKey.Contains("YOUR_") ? "active" : "disabled",
        });

        var edgeTtsUrl = _config["EdgeTts:BaseUrl"] ?? "";
        integrations.Add(new
        {
            name = "EdgeTTS",
            category = "ai",
            enabled = !string.IsNullOrEmpty(edgeTtsUrl),
            configured = !string.IsNullOrEmpty(edgeTtsUrl),
            config = new { baseUrl = edgeTtsUrl },
            status = !string.IsNullOrEmpty(edgeTtsUrl) ? "active" : "disabled",
        });

        var bunnyCdnKey = _config["BunnyCdn:ApiKey"] ?? "";
        integrations.Add(new
        {
            name = "BunnyCDN",
            category = "cdn",
            enabled = !string.IsNullOrEmpty(bunnyCdnKey),
            configured = !string.IsNullOrEmpty(bunnyCdnKey),
            config = new { keyHint = bunnyCdnKey.Length > 8 ? bunnyCdnKey[..4] + "..." : "not set" },
            status = !string.IsNullOrEmpty(bunnyCdnKey) ? "active" : "disabled",
        });

        return (200, new { integrations });
    }

    public (int, object?) GetPlatformInfo()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        return Ok(new
        {
            application = "MURO",
            version = "2.0.0",
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            startedAt = process.StartTime.ToUniversalTime(),
            uptime = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).ToString(@"d\.hh\:mm\:ss"),
            memoryMb = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1),
            threadCount = process.Threads.Count,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            machineName = Environment.MachineName,
        });
    }

    public (int, object?) GetWebhookConfig()
    {
        return Ok(new
        {
            webhooks = new[]
            {
                new { name = "BBB Webhook", key = "Bbb:WebhookSharedSecret", configured = !string.IsNullOrEmpty(_config["Bbb:WebhookSharedSecret"]), hint = MaskSecret(_config["Bbb:WebhookSharedSecret"]) },
                new { name = "Platform Webhook", key = "Webhook:Secret", configured = !string.IsNullOrEmpty(_config["Webhook:Secret"]), hint = MaskSecret(_config["Webhook:Secret"]) },
                new { name = "VEP Webhook URL", key = "Vep:WebhookUrl", configured = !string.IsNullOrEmpty(_config["Vep:WebhookUrl"]), hint = _config["Vep:WebhookUrl"] ?? "not set" },
                new { name = "VEP Webhook Secret", key = "Vep:WebhookSecret", configured = !string.IsNullOrEmpty(_config["Vep:WebhookSecret"]), hint = MaskSecret(_config["Vep:WebhookSecret"]) },
            },
        });
    }

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "not set";
        if (value.Length <= 8) return "***";
        return value[..4] + "..." + value[^4..];
    }

    private static string MaskConnectionString(string connStr)
    {
        if (string.IsNullOrEmpty(connStr)) return "not set";
        var masked = System.Text.RegularExpressions.Regex.Replace(
            connStr, @"(Password|Pwd)\s*=\s*[^;]+", "Password=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return masked;
    }
}
