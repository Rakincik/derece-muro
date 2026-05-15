using MURO.Application.DTOs.Admin;

namespace MURO.Application.Interfaces;

public interface IAdminHealthService
{
    Task<(int, object?)> GetBbbHealth();
    Task<(int, object?)> GetServiceHealth();
    Task<(int, object?)> GetDbPools();
    Task<(int, object?)> TestDbConnection(DbTestRequest request);
}
