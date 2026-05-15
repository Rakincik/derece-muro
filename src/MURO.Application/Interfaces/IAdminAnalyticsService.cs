using MURO.Application.DTOs.Admin;

namespace MURO.Application.Interfaces;

public interface IAdminAnalyticsService
{
    Task<(int, object?)> GetSessionHistory(int days = 7);
    Task<(int, object?)> GetSessionHeatmap(int weeks = 4);
}
