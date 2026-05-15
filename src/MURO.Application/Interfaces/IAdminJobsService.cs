using MURO.Application.DTOs.Admin;

namespace MURO.Application.Interfaces;

public interface IAdminJobsService
{
    Task<(int, object?)> GetJobStatus();
    Task<(int, object?)> GetErrorQueue();
    Task<(int, object?)> RetryFailedItem(Guid id, RetryRequest request);
}
