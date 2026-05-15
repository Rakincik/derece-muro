using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MURO.Infrastructure.Persistence;

/// <summary>
/// Design-time factory — EF migrations oluşturmak için.
/// Redis/SignalR gibi runtime bağımlılıklara ihtiyaç duymaz.
/// 
/// Kullanım:
///   dotnet ef migrations add MigrationName --project src/MURO.Infrastructure --startup-project src/MURO.API
/// </summary>
public class MuroDbContextDesignTimeFactory : IDesignTimeDbContextFactory<MuroDbContext>
{
    public MuroDbContext CreateDbContext(string[] args)
    {
        // Environment variable veya fallback connection string
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=muro_dev;Username=muro_user;Password=muro_pass_2024";

        var optionsBuilder = new DbContextOptionsBuilder<MuroDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new MuroDbContext(optionsBuilder.Options);
    }
}

