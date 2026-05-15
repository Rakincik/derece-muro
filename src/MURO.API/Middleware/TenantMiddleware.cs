using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Tenants;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;

namespace MURO.API.Middleware;

/// <summary>
/// Resolves the current tenant from (1) subdomain, (2) X-Tenant-Id header, or (3) JWT claim.
/// Loads full TenantInfo so downstream services have branding, connection string, and feature flags.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _baseDomain;

    public TenantMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        // e.g., "muro.com.tr" — subdomains are resolved against this
        _baseDomain = configuration["Tenant:BaseDomain"] ?? "localhost";
    }

    public async Task InvokeAsync(HttpContext context, ITenantService tenantService, MuroDbContext masterDb)
    {
        // Skip tenant resolution for health checks, swagger, etc.
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/swagger") || path.StartsWith("/health"))
        {
            await _next(context);
            return;
        }

        TenantInfo? tenantInfo = null;

        // ── Priority 1: Subdomain ──
        var host = context.Request.Host.Host; // e.g., "abckpss.muro.com.tr" or "abckpss.localhost"
        var subdomain = ExtractSubdomain(host);

        if (!string.IsNullOrEmpty(subdomain))
        {
            tenantInfo = await ResolveTenantBySubdomain(masterDb, subdomain);
        }

        // ── Priority 2: X-Tenant-Id header ──
        if (tenantInfo == null)
        {
            var tenantIdHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tenantIdHeader) && Guid.TryParse(tenantIdHeader, out var headerTenantId))
            {
                tenantInfo = await ResolveTenantById(masterDb, headerTenantId);
            }
        }

        // ── Priority 3: JWT claim ──
        if (tenantInfo == null && context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenantId")?.Value;
            if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var claimTenantId))
            {
                tenantInfo = await ResolveTenantById(masterDb, claimTenantId);
            }
        }

        // Set tenant context
        if (tenantInfo != null)
        {
            tenantService.SetCurrentTenant(tenantInfo);
        }

        await _next(context);
    }

    private string? ExtractSubdomain(string host)
    {
        // Remove port if present: "abckpss.localhost:5292" → "abckpss.localhost"
        var hostWithoutPort = host.Split(':')[0];

        // For development: "abckpss.localhost" → "abckpss"
        if (hostWithoutPort.EndsWith(".localhost"))
        {
            var sub = hostWithoutPort.Replace(".localhost", "");
            return string.IsNullOrEmpty(sub) ? null : sub;
        }

        // For production: "abckpss.muro.com.tr" → "abckpss"
        if (!string.IsNullOrEmpty(_baseDomain) && hostWithoutPort.EndsWith($".{_baseDomain}"))
        {
            var sub = hostWithoutPort[..^(_baseDomain.Length + 1)];
            return string.IsNullOrEmpty(sub) ? null : sub;
        }

        return null;
    }

    private static async Task<TenantInfo?> ResolveTenantBySubdomain(MuroDbContext db, string subdomain)
    {
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Subdomain == subdomain && t.IsActive);

        return tenant == null ? null : MapToTenantInfo(tenant);
    }

    private static async Task<TenantInfo?> ResolveTenantById(MuroDbContext db, Guid tenantId)
    {
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);

        return tenant == null ? null : MapToTenantInfo(tenant);
    }

    private static TenantInfo MapToTenantInfo(Domain.Entities.Tenant t)
    {
        var features = new Dictionary<string, bool>();
        if (!string.IsNullOrWhiteSpace(t.Features))
        {
            try
            {
                features = JsonSerializer.Deserialize<Dictionary<string, bool>>(t.Features)
                    ?? new Dictionary<string, bool>();
            }
            catch { /* ignore malformed JSON */ }
        }

        return new TenantInfo(
            Id: t.Id,
            Name: t.Name,
            Subdomain: t.Subdomain,
            LogoUrl: t.LogoUrl,
            FaviconUrl: t.FaviconUrl,
            PrimaryColor: t.PrimaryColor,
            AccentColor: t.AccentColor,
            FooterText: t.FooterText,
            BbbServerUrl: t.BbbServerUrl,
            BbbSecret: t.BbbSecret,
            ConnectionString: t.ConnectionString,
            IsActive: t.IsActive,
            Features: features
        );
    }
}
