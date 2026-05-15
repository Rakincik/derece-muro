namespace MURO.Application.Interfaces;

public interface IAdminBackupService
{
    Task<(int, object?)> TriggerBackup();
    (int, object?) GetBackupPolicy();
    (int, object?) GetBackupJobs();
    (int, object?) GetRetentionConfig();
    (int, object?) CleanOldBackups(int olderThanDays = 30);
}
