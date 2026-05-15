using MURO.Application.DTOs.Admin;

namespace MURO.Application.Interfaces;

public interface IPlatformAnalyticsService
{
    Task<PlatformStatsReport> GetPlatformStatsAsync();
}

public class PlatformStatsReport
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Kullanıcılar
    public int TotalStudents { get; set; }
    public int TotalTeachers { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsersToday { get; set; }
    public string? LastLoginAt { get; set; }

    // Eğitim
    public int TotalCourses { get; set; }
    public int PublishedCourses { get; set; }
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }

    // Kayıtlar
    public int TotalRecordings { get; set; }
    public int PendingRecordings { get; set; }
    public int ProcessingRecordings { get; set; }
    public int CompletedRecordingsToday { get; set; }

    // Medya
    public int TotalMediaAssets { get; set; }

    // Kurumlar
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }

    // Paketler
    public int TotalPackages { get; set; }
    public int ActiveUserPackages { get; set; }
}
