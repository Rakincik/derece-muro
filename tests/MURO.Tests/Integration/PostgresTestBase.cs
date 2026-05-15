#pragma warning disable CS0618
using Microsoft.EntityFrameworkCore;
using MURO.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace MURO.Tests.Integration;

/// <summary>
/// TestContainers ile gerçek PostgreSQL üzerinde çalışan test altyapısı.
/// Docker gerektirir — CI/CD pipeline'da da kullanılabilir.
/// </summary>
public class PostgresTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    protected MuroDbContext Db { get; private set; } = null!;

    public PostgresTestBase()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("muro_test")
            .WithUsername("muro_test")
            .WithPassword("muro_test_pw")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        Db = new MuroDbContext(options);
        await Db.Database.MigrateAsync(); // Gerçek migration uygula
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
