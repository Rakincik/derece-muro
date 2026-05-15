using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MURO.Application.DTOs.Admin;
using MURO.Application.Interfaces;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class AdminHealthService : IAdminHealthService
{
    private readonly MuroDbContext _db;
    private readonly IBbbService _bbb;
    private readonly IConfiguration _config;

    public AdminHealthService(MuroDbContext db, IBbbService bbb, IConfiguration config)
    {
        _db = db;
        _bbb = bbb;
        _config = config;
    }

    private (int, object?) Ok(object? data = null) => (200, data);

    public async Task<(int, object?)> GetBbbHealth()
    {
        var meetings = await _bbb.GetMeetingsAsync();
        var today = DateTime.UtcNow.Date;
        var todaySessionCount = await _db.Sessions.CountAsync(s => s.ScheduledStart >= today);
        var totalRecordings = await _db.SessionRecordings.CountAsync();
        var processingRecordings = await _db.SessionRecordings.CountAsync(r => r.Status == MediaStatus.Processing);

        var totalParticipants = meetings.Sum(m => m.ParticipantCount);
        var totalModerators = meetings.Sum(m => m.ModeratorCount);
        var totalVideos = meetings.Sum(m => m.VideoCount);
        var totalVoice = meetings.Sum(m => m.VoiceParticipantCount);
        var recordingCount = meetings.Count(m => m.IsRecording);

        return Ok(new
        {
            status = meetings.Count > 0 ? "active" : "idle",
            activeMeetings = meetings.Count,
            totalParticipants,
            totalModerators,
            totalVideos,
            totalVoice,
            recordingMeetings = recordingCount,
            todaySessionCount,
            totalRecordings,
            processingRecordings,
            meetings = meetings.Select(m => new
            {
                m.MeetingId,
                m.MeetingName,
                m.ParticipantCount,
                m.ModeratorCount,
                m.ListenerCount,
                m.VideoCount,
                m.VoiceParticipantCount,
                m.IsRecording,
                m.IsBreakout,
                m.StartTime,
                m.Duration,
            }),
        });
    }

    public async Task<(int, object?)> GetServiceHealth()
    {
        var services = new List<object>();

        try
        {
            var start = DateTime.UtcNow;
            await _db.Database.ExecuteSqlRawAsync("SELECT 1");
            var latency = (DateTime.UtcNow - start).TotalMilliseconds;

            services.Add(new
            {
                name = "PostgreSQL",
                status = "online",
                latencyMs = Math.Round(latency, 1),
                details = new { connectionCount = 0, dbName = _db.Database.GetDbConnection().Database },
            });
        }
        catch (Exception ex)
        {
            services.Add(new { name = "PostgreSQL", status = "offline", latencyMs = 0.0, details = new { error = ex.Message } });
        }

        services.Add(new
        {
            name = "MURO API",
            status = "online",
            latencyMs = 0.0,
            details = new { pid = Environment.ProcessId, startTime = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime() },
        });

        try
        {
            var bbbUrl = _config["Bbb:ServerUrl"];
            if (!string.IsNullOrEmpty(bbbUrl))
            {
                var start = DateTime.UtcNow;
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var resp = await http.GetAsync($"{bbbUrl}api");
                var latency = (DateTime.UtcNow - start).TotalMilliseconds;
                services.Add(new
                {
                    name = "BigBlueButton",
                    status = resp.IsSuccessStatusCode ? "online" : "degraded",
                    latencyMs = Math.Round(latency, 1),
                    details = new { url = bbbUrl, statusCode = (int)resp.StatusCode },
                });
            }
            else
            {
                services.Add(new { name = "BigBlueButton", status = "not_configured", latencyMs = 0.0, details = new { url = (string?)null, statusCode = 0 } });
            }
        }
        catch (Exception)
        {
            services.Add(new { name = "BigBlueButton", status = "offline", latencyMs = 0.0, details = new { url = _config["Bbb:ServerUrl"], statusCode = 0 } });
        }

        try
        {
            var redisConn = _config.GetConnectionString("Redis") ?? _config["Redis:ConnectionString"];
            services.Add(new
            {
                name = "Redis",
                status = !string.IsNullOrEmpty(redisConn) ? "configured" : "not_configured",
                latencyMs = 0.0,
                details = new { connectionString = !string.IsNullOrEmpty(redisConn) ? "***configured***" : "not set" },
            });
        }
        catch
        {
            services.Add(new { name = "Redis", status = "unknown", latencyMs = 0.0, details = new { connectionString = "error" } });
        }

        var lastProcessed = await _db.MediaAssets
            .Where(a => a.Status == MediaStatus.Ready)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        services.Add(new
        {
            name = "MURO Worker",
            status = lastProcessed > DateTime.UtcNow.AddMinutes(-30) ? "active" : lastProcessed != default ? "idle" : "unknown",
            latencyMs = 0.0,
            details = new { lastProcessedAt = lastProcessed != default ? lastProcessed.ToString("O") : null },
        });

        return (200, new { services, checkedAt = DateTime.UtcNow });
    }

    public async Task<(int, object?)> GetDbPools()
    {
        var mainConnStr = _config.GetConnectionString("DefaultConnection") ?? "";
        var mainHost = ExtractHost(mainConnStr);

        var tenantConns = await _db.Tenants
            .AsNoTracking()
            .Select(t => new { t.Id, t.Name, t.Code, t.ConnectionString, t.ServerGroup })
            .ToListAsync();

        var pools = new List<object>();
        var mainTenants = tenantConns.Where(t => string.IsNullOrEmpty(t.ConnectionString)).ToList();
        
        pools.Add(new
        {
            id = "main",
            host = mainHost,
            label = "Ana Sunucu (Shared)",
            type = "shared",
            tenantCount = mainTenants.Count,
            tenants = mainTenants.Select(t => new { t.Id, t.Name, t.Code }),
            connectionHint = MaskConnectionString(mainConnStr),
            status = "online",
        });

        var dedicatedGroups = tenantConns
            .Where(t => !string.IsNullOrEmpty(t.ConnectionString))
            .GroupBy(t => ExtractHost(t.ConnectionString!))
            .ToList();

        foreach (var group in dedicatedGroups)
        {
            pools.Add(new
            {
                id = group.Key?.Replace(".", "-") ?? "unknown",
                host = group.Key,
                label = $"Dedicated ({group.Key})",
                type = "dedicated",
                tenantCount = group.Count(),
                tenants = group.Select(t => new { t.Id, t.Name, t.Code }),
                connectionHint = MaskConnectionString(group.First().ConnectionString ?? ""),
                status = "unknown",
            });
        }

        return (200, new { pools, totalServers = pools.Count, totalTenants = tenantConns.Count });
    }

    public async Task<(int, object?)> TestDbConnection(DbTestRequest request)
    {
        try
        {
            using var conn = new Npgsql.NpgsqlConnection(request.ConnectionString);
            var start = DateTime.UtcNow;
            await conn.OpenAsync();
            var latency = (DateTime.UtcNow - start).TotalMilliseconds;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT pg_database_size(current_database()), current_database(), version()";
            using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            var dbSizeBytes = reader.GetInt64(0);
            var dbName = reader.GetString(1);
            var version = reader.GetString(2);

            return Ok(new
            {
                success = true,
                latencyMs = Math.Round(latency, 1),
                database = dbName,
                sizeBytes = dbSizeBytes,
                sizeMb = Math.Round(dbSizeBytes / 1048576.0, 1),
                version,
            });
        }
        catch (Exception ex)
        {
            return (200, new { success = false, error = ex.Message });
        }
    }

    private static string MaskConnectionString(string connStr)
    {
        if (string.IsNullOrEmpty(connStr)) return "not set";
        var masked = System.Text.RegularExpressions.Regex.Replace(
            connStr, @"(Password|Pwd)\s*=\s*[^;]+", "Password=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return masked;
    }

    private static string? ExtractHost(string? connStr)
    {
        if (string.IsNullOrEmpty(connStr)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(connStr, @"(Host|Server)\s*=\s*([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[2].Value.Trim() : "unknown";
    }
}
