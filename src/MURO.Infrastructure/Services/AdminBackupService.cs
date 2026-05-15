using Microsoft.Extensions.Configuration;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Services;

public class AdminBackupService : IAdminBackupService
{
    private readonly IConfiguration _config;

    public AdminBackupService(IConfiguration config)
    {
        _config = config;
    }

    private (int, object?) Ok(object? data = null) => (200, data);

    public (int, object?) GetBackupPolicy()
    {
        var connStr = _config.GetConnectionString("DefaultConnection") ?? "";
        var dbName = ExtractDbName(connStr);

        return Ok(new
        {
            database = new
            {
                dbName,
                engine = "PostgreSQL",
                method = "pg_dump + gzip",
                schedule = "GÃ¼nlÃ¼k 03:00 UTC",
                target = GetBackupDir(),
                compressionEnabled = true,
                includeSchema = true,
                includeData = true,
            },
            media = new
            {
                method = "BunnyCDN replication",
                enabled = !string.IsNullOrEmpty(_config["BunnyCdn:ApiKey"]),
                note = "CDN media dosyalarÄ± otomatik replike edilir",
            },
            retention = new
            {
                dailyKeepDays = 7,
                weeklyKeepWeeks = 4,
                monthlyKeepMonths = 3,
            },
        });
    }

    public (int, object?) GetBackupJobs()
    {
        var dir = GetBackupDir();
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var files = Directory.GetFiles(dir, "*.sql*")
            .Concat(Directory.GetFiles(dir, "*.gz"))
            .Concat(Directory.GetFiles(dir, "*.dump"))
            .Distinct()
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Take(50)
            .Select(f => new
            {
                id = f.Name.GetHashCode().ToString("x8"),
                fileName = f.Name,
                fullPath = f.FullName,
                sizeBytes = f.Length,
                sizeMb = Math.Round(f.Length / 1024.0 / 1024.0, 2),
                createdAt = f.CreationTimeUtc,
                status = "completed",
                type = f.Extension.Contains("gz") ? "compressed" : "plain",
            })
            .ToList();

        var totalSizeMb = files.Sum(f => f.sizeMb);

        return (200, new { items = files, count = files.Count, totalSizeMb });
    }

    public async Task<(int, object?)> TriggerBackup()
    {
        var connStr = _config.GetConnectionString("DefaultConnection") ?? "";
        var dbName = ExtractDbName(connStr);
        var host = ExtractConnParam(connStr, "Host") ?? "localhost";
        var port = ExtractConnParam(connStr, "Port") ?? "5432";
        var user = ExtractConnParam(connStr, "Username") ?? "postgres";
        var pass = ExtractConnParam(connStr, "Password") ?? "";

        var dir = GetBackupDir();
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"muro_backup_{timestamp}.sql.gz";
        var filePath = Path.Combine(dir, fileName);

        try
        {
            var pgDumpPath = "pg_dump";
            var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows);

            var startInfo = new System.Diagnostics.ProcessStartInfo();

            if (isWindows)
            {
                var plainFile = Path.Combine(dir, $"muro_backup_{timestamp}.sql");
                startInfo.FileName = pgDumpPath;
                startInfo.Arguments = $"-h {host} -p {port} -U {user} -d {dbName} -f \"{plainFile}\"";
                startInfo.Environment["PGPASSWORD"] = pass;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardError = true;
                startInfo.CreateNoWindow = true;

                var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    var stderr = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                        return (200, new { success = false, error = $"pg_dump failed: {stderr}", fileName });

                    var fileInfo = new FileInfo(plainFile);
                    return Ok(new
                    {
                        success = true,
                        fileName = Path.GetFileName(plainFile),
                        sizeMb = Math.Round(fileInfo.Length / 1024.0 / 1024.0, 2),
                        path = plainFile,
                        duration = "completed",
                    });
                }
            }
            else
            {
                startInfo.FileName = "/bin/bash";
                startInfo.Arguments = $"-c \"PGPASSWORD='{pass}' pg_dump -h {host} -p {port} -U {user} {dbName} | gzip > {filePath}\"";
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardError = true;
                startInfo.CreateNoWindow = true;

                var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    var stderr = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                        return (200, new { success = false, error = $"pg_dump failed: {stderr}", fileName });

                    var fileInfo = new FileInfo(filePath);
                    return Ok(new
                    {
                        success = true,
                        fileName,
                        sizeMb = Math.Round(fileInfo.Length / 1024.0 / 1024.0, 2),
                        path = filePath,
                        duration = "completed",
                    });
                }
            }

            return (200, new { success = false, error = "Process baÅŸlatÄ±lamadÄ±" });
        }
        catch (Exception ex)
        {
            return (200, new { success = false, error = ex.Message, fileName });
        }
    }

    public (int, object?) GetRetentionConfig()
    {
        var dir = GetBackupDir();
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var files = Directory.GetFiles(dir)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        var totalSizeMb = Math.Round(files.Sum(f => f.Length) / 1024.0 / 1024.0, 2);
        var oldestBackup = files.LastOrDefault()?.CreationTimeUtc;
        var newestBackup = files.FirstOrDefault()?.CreationTimeUtc;

        var now = DateTime.UtcNow;
        var last24h = files.Count(f => (now - f.CreationTimeUtc).TotalHours <= 24);
        var last7d = files.Count(f => (now - f.CreationTimeUtc).TotalDays <= 7);
        var last30d = files.Count(f => (now - f.CreationTimeUtc).TotalDays <= 30);
        var older = files.Count(f => (now - f.CreationTimeUtc).TotalDays > 30);

        return Ok(new
        {
            rules = new
            {
                dailyKeepDays = 7,
                weeklyKeepWeeks = 4,
                monthlyKeepMonths = 3,
                maxTotalSizeGb = 10,
            },
            stats = new
            {
                totalFiles = files.Count,
                totalSizeMb,
                oldestBackup,
                newestBackup,
                last24h,
                last7d,
                last30d,
                older,
            },
        });
    }

    public (int, object?) CleanOldBackups(int olderThanDays = 30)
    {
        var dir = GetBackupDir();
        if (!Directory.Exists(dir))
            return (200, new { deleted = 0, message = "Backup dizini bulunamadÄ±" });

        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
        var files = Directory.GetFiles(dir)
            .Select(f => new FileInfo(f))
            .Where(f => f.CreationTimeUtc < cutoff)
            .ToList();

        long freedBytes = 0;
        foreach (var f in files)
        {
            freedBytes += f.Length;
            f.Delete();
        }

        return Ok(new
        {
            deleted = files.Count,
            freedMb = Math.Round(freedBytes / 1024.0 / 1024.0, 2),
            message = $"{files.Count} eski backup silindi ({Math.Round(freedBytes / 1024.0 / 1024.0, 2)} MB)",
        });
    }

    private string GetBackupDir()
    {
        var dir = _config["Backup:Directory"] ?? "/var/backups/muro";
        if (!System.IO.Directory.Exists(dir))
        {
            try { System.IO.Directory.CreateDirectory(dir); } catch { }
        }
        return dir;
    }

    private static string ExtractDbName(string connStr)
    {
        var match = System.Text.RegularExpressions.Regex.Match(connStr, @"(Database)\s*=\s*([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[2].Value.Trim() : "unknown";
    }

    private static string? ExtractConnParam(string connStr, string paramName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(connStr, $@"({paramName})\s*=\s*([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[2].Value.Trim() : null;
    }
}
