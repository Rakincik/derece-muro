using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MURO.Infrastructure.Persistence;

/// <summary>
/// Creates tenant-specific MuroDbContext instances — resolves to the tenant's own database
/// when a tenant ConnectionString is set, otherwise falls back to the default connection.
/// </summary>
public interface ITenantDbContextFactory
{
    /// <summary>Create a DbContext for the current tenant (or the default DB).</summary>
    MuroDbContext CreateContext();
}

public class TenantDbContextFactory : ITenantDbContextFactory
{
    private readonly Application.Interfaces.ITenantService _tenantService;
    private readonly string _defaultConnectionString;

    public TenantDbContextFactory(
        Application.Interfaces.ITenantService tenantService,
        IConfiguration configuration)
    {
        _tenantService = tenantService;
        _defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");
    }

    public MuroDbContext CreateContext()
    {
        var connectionString = _tenantService.GetConnectionString() ?? _defaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<MuroDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new MuroDbContext(optionsBuilder.Options);
    }
}
