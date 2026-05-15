using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Persistence;
using StackExchange.Redis;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Sistem sağlık ve metrik servisi — VEP Control Plane entegrasyonu.
/// DB, Redis, BBB durumlarını kontrol eder; platform istatistiklerini toplar.
/// </summary>
public class SystemHealthService : ISystemHealthService
{
    private readonly MuroDbContext _db;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IBbbService _bbb;
    private readonly IConfiguration _config;
    private readonly ILogger<SystemHealthService> _logger;

    private static readonly DateTime _startTime = DateTime.UtcNow;

    public SystemHealthService(
        MuroDbContext db,
        IConfiguration config,
        IBbbService bbb,
        ILogger<SystemHealthService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _db = db;
        _redis = redis;
        _bbb = bbb;
        _config = config;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HEALTH REPORT
    // ═══════════════════════════════════════════════════════════════════

    public async Task<SystemHealthReport> GetHealthReportAsync()
    {
        var report = new SystemHealthReport
        {
            Version = typeof(SystemHealthService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            UptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds,
            Timestamp = DateTime.UtcNow,
        };

        // API
        report.Api = new ServiceHealth
        {
            Status = "ok",
            ResponseMs = 0,
            Details = new Dictionary<string, object>
            {
                ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                ["runtime"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            }
        };

        // Database
        report.Database = await CheckDatabaseAsync();

        // Redis
        report.Redis = await CheckRedisAsync();

        // BBB
        report.Bbb = await CheckBbbAsync();

        // Worker (Hangfire job durumu — basit DB kontrolü)
        report.Worker = await CheckWorkerAsync();

        // Storage
        report.Storage = await GetStorageHealthAsync();

        // Overall
        var statuses = new[] { report.Database.Status, report.Redis.Status, report.Bbb.Status };
        if (statuses.All(s => s == "ok" || s == "disabled"))
            report.OverallStatus = "healthy";
        else if (statuses.Any(s => s == "error"))
            report.OverallStatus = "unhealthy";
        else
            report.OverallStatus = "degraded";

        return report;
    }

    public async Task<bool> CheckReadinessAsync()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            if (!canConnect) return false;

            if (_redis != null && _redis.IsConnected)
            {
                var db = _redis.GetDatabase();
                await db.PingAsync();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── DB Check ────────────────────────────────────────────────────────

    private async Task<ServiceHealth> CheckDatabaseAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            sw.Stop();

            if (!canConnect)
                return new ServiceHealth { Status = "error", ResponseMs = (int)sw.ElapsedMilliseconds, Error = "Cannot connect" };

            // Detaylar: aktif bağlantılar, veritabanı boyutu
            var dbName = _db.Database.GetDbConnection().Database;
            var activeConns = 0;
            double dbSizeGb = 0;

            try
            {
                // Yeni bir raw bağlantı aç (DbContext'in paylaşılan bağlantısını kullanma!)
                var connStr = _db.Database.GetConnectionString();
                using var conn = new Npgsql.NpgsqlConnection(connStr);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database()";
                activeConns = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);

                cmd.CommandText = "SELECT pg_database_size(current_database()) / (1024.0 * 1024 * 1024)";
                dbSizeGb = Math.Round(Convert.ToDouble(await cmd.ExecuteScalarAsync() ?? 0), 2);
            }
            catch { /* detail query failed — not critical */ }

            return new ServiceHealth
            {
                Status = "ok",
                ResponseMs = (int)sw.ElapsedMilliseconds,
                Details = new Dictionary<string, object>
                {
                    ["database"] = dbName ?? "",
                    ["activeConnections"] = activeConns,
                    ["sizeGb"] = dbSizeGb,
                }
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Database health check failed");
            return new ServiceHealth { Status = "error", ResponseMs = (int)sw.ElapsedMilliseconds, Error = ex.Message };
        }
    }

    // ── Redis Check ──────────────────────────────────────────────────────

    private async Task<ServiceHealth> CheckRedisAsync()
    {
        if (_redis is null || !_redis.IsConnected)
        {
            var redisConnStr = _config.GetConnectionString("Redis");
            if (string.IsNullOrEmpty(redisConnStr))
                return new ServiceHealth { Status = "disabled" };

            return new ServiceHealth { Status = "error", Error = "Not connected" };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var db = _redis.GetDatabase();
            await db.PingAsync();
            sw.Stop();

            var server = _redis.GetServer(_redis.GetEndPoints().First());

            long memoryMb = 0;
            int connectedClients = 0;
            double hitRate = 0;

            // INFO komutu admin mode gerektirebilir — hata olursa sadece PING sonucunu döndür
            try
            {
                var info = await server.InfoAsync("memory");
                var clients = await server.InfoAsync("clients");
                var stats = await server.InfoAsync("stats");

                foreach (var group in info)
                    foreach (var item in group)
                    {
                        if (item.Key == "used_memory")
                            memoryMb = long.Parse(item.Value) / (1024 * 1024);
                    }

                foreach (var group in clients)
                    foreach (var item in group)
                    {
                        if (item.Key == "connected_clients")
                            connectedClients = int.Parse(item.Value);
                    }

                long hits = 0, misses = 0;
                foreach (var group in stats)
                    foreach (var item in group)
                    {
                        if (item.Key == "keyspace_hits") hits = long.Parse(item.Value);
                        if (item.Key == "keyspace_misses") misses = long.Parse(item.Value);
                    }
                hitRate = (hits + misses) > 0 ? Math.Round((double)hits / (hits + misses), 2) : 0;
            }
            catch (Exception infoEx)
            {
                _logger.LogDebug(infoEx, "Redis INFO command failed (admin mode may be required) — using PING only");
            }

            return new ServiceHealth
            {
                Status = "ok",
                ResponseMs = (int)sw.ElapsedMilliseconds,
                Details = new Dictionary<string, object>
                {
                    ["memoryMb"] = memoryMb,
                    ["connectedClients"] = connectedClients,
                    ["hitRate"] = hitRate,
                }
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Redis health check failed");
            return new ServiceHealth { Status = "error", ResponseMs = (int)sw.ElapsedMilliseconds, Error = ex.Message };
        }
    }

    // ── BBB Check ────────────────────────────────────────────────────────

    private async Task<ServiceHealth> CheckBbbAsync()
    {
        var bbbUrl = _config["Bbb:Url"];
        if (string.IsNullOrEmpty(bbbUrl))
            return new ServiceHealth { Status = "disabled" };

        var sw = Stopwatch.StartNew();
        try
        {
            // IsMeetingRunning — BBB API erişilebilirliğini test eder
            // Mevcut olmayan bir meeting ID ile çağrı yaparak API bağlantısını doğrular
            await _bbb.IsMeetingRunningAsync("health-check-probe");
            sw.Stop();

            return new ServiceHealth
            {
                Status = "ok",
                ResponseMs = (int)sw.ElapsedMilliseconds,
                Details = new Dictionary<string, object>
                {
                    ["serverUrl"] = bbbUrl,
                }
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "BBB health check failed");
            return new ServiceHealth { Status = "error", ResponseMs = (int)sw.ElapsedMilliseconds, Error = ex.Message };
        }
    }

    // ── Worker Check ─────────────────────────────────────────────────────

    private async Task<ServiceHealth> CheckWorkerAsync()
    {
        // Worker durumu: son 1 saat içinde işlenmiş kayıt var mı?
        try
        {
            var recentRecordings = await _db.SessionRecordings
                .Where(r => r.CreatedAt > DateTime.UtcNow.AddHours(-1))
                .CountAsync();

            return new ServiceHealth
            {
                Status = "ok",
                Details = new Dictionary<string, object>
                {
                    ["recentRecordings1h"] = recentRecordings,
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceHealth { Status = "error", Error = ex.Message };
        }
    }

    // ── Storage Health ───────────────────────────────────────────────────

    private async Task<StorageHealth> GetStorageHealthAsync()
    {
        try
        {
            var totalMedia = await _db.MediaAssets.CountAsync();
            var totalRecordings = await _db.SessionRecordings.CountAsync();
            var pendingRecordings = await _db.SessionRecordings
                .Where(r => r.Status == Domain.Enums.MediaStatus.Processing)
                .CountAsync();

            return new StorageHealth
            {
                Status = "ok",
                TotalMediaAssets = totalMedia,
                TotalRecordings = totalRecordings,
                PendingRecordings = pendingRecordings,
            };
        }
        catch (Exception)
        {
            return new StorageHealth { Status = "error" };
        }
    }

}
