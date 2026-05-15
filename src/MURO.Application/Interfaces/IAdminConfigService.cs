namespace MURO.Application.Interfaces;

public interface IAdminConfigService
{
    Task<(int, object?)> GetStorageStats();
    Task<(int, object?)> GetSecrets();
    (int, object?) GetIntegrationStatus();
    (int, object?) GetGeneralSettings();
    (int, object?) GetPlatformInfo();
    (int, object?) GetWebhookConfig();
}
