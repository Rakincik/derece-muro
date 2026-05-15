using MURO.Application.DTOs.Accounting;

namespace MURO.Application.Interfaces;

public interface IAccountingService
{
    Task<AccountingSummaryDto> GetSummaryAsync(Guid tenantId, DateTime? from, DateTime? to);
    
    Task<List<TransactionDto>> GetTransactionsAsync(Guid tenantId, string? type, string? status, Guid? planId, DateTime? from, DateTime? to, int page, int pageSize);
    
    Task<TransactionDto> CreateTransactionAsync(Guid tenantId, Guid userId, CreateTransactionRequest req, string? ipAddress);
    
    Task<bool> DeleteTransactionAsync(Guid tenantId, Guid userId, Guid transactionId, string? ipAddress);

    Task<List<PlanDto>> GetPlansAsync(Guid tenantId);

    Task<PlanDto> CreatePlanAsync(Guid tenantId, Guid userId, CreatePlanRequest req, string? ipAddress);

    Task<bool> DeletePlanAsync(Guid tenantId, Guid userId, Guid planId, string? ipAddress);
}
