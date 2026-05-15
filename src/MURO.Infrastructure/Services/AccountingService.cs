using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Accounting;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Infrastructure.Persistence;

namespace MURO.Infrastructure.Services;

public class AccountingService : IAccountingService
{
    private readonly MuroDbContext _db;
    private readonly IBackgroundJobQueue _jobQueue;

    public AccountingService(MuroDbContext db, IBackgroundJobQueue jobQueue)
    {
        _db = db;
        _jobQueue = jobQueue;
    }

    public async Task<AccountingSummaryDto> GetSummaryAsync(Guid tenantId, DateTime? from, DateTime? to)
    {
        var q = _db.Transactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Include(t => t.Plan)
            .AsQueryable();

        if (from.HasValue) q = q.Where(t => t.TransactionDate >= from.Value);
        if (to.HasValue) q = q.Where(t => t.TransactionDate <= to.Value);

        var txs = await q.ToListAsync();

        var sales = txs.Where(t => t.Type == "sale" && t.Status == "paid").ToList();
        var refunds = txs.Where(t => t.Type == "refund" && t.Status == "paid").ToList();
        var expenses = txs.Where(t => t.Type == "expense" && t.Status == "paid").ToList();

        decimal totalRevenue = sales.Sum(t => t.Amount);
        decimal totalRefunds = refunds.Sum(t => t.Amount);
        decimal totalExpenses = expenses.Sum(t => t.Amount);

        // Pending total
        var pendingTxs = txs.Where(t => t.Status == "pending").ToList();
        decimal pendingTotal = pendingTxs.Sum(t => t.Amount);

        // Monthly (last 12 months)
        var monthFrom = DateTime.UtcNow.AddMonths(-11);
        var monthly = txs
            .Where(t => t.TransactionDate >= monthFrom)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new MonthlyRevenueDto(
                g.Key.Year, g.Key.Month,
                new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM", new System.Globalization.CultureInfo("tr-TR")),
                g.Where(t => t.Type == "sale" && t.Status == "paid").Sum(t => t.Amount),
                g.Where(t => t.Type == "refund" && t.Status == "paid").Sum(t => t.Amount),
                g.Where(t => t.Type == "expense" && t.Status == "paid").Sum(t => t.Amount)
            ))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        // Last 3 month average
        var last3 = monthly.TakeLast(3).ToList();
        decimal last3MonthAvg = last3.Count > 0
            ? Math.Round(last3.Sum(m => m.Revenue) / last3.Count, 0)
            : 0;

        // Plan breakdown
        var colors = new[] { "#6366f1", "#8b5cf6", "#ec4899", "#10b981", "#f59e0b", "#3b82f6", "#ef4444", "#06b6d4" };
        var planBreakdown = sales
            .GroupBy(t => t.Plan?.Name ?? "Diğer")
            .Select((g, i) => new PlanRevenueDto(g.Key, g.Sum(t => t.Amount), g.Count(), colors[i % colors.Length]))
            .OrderByDescending(p => p.Revenue)
            .ToList();

        // Payment method breakdown
        var pmLabels = new Dictionary<string, string>
        {
            { "card", "Kredi Kartı" }, { "bank_transfer", "Havale/EFT" },
            { "cash", "Nakit" }, { "other", "Diğer" }
        };
        var saleTxs = txs.Where(t => t.Type == "sale").ToList();
        var totalSaleAmount = saleTxs.Sum(t => t.Amount);
        var pmBreakdown = saleTxs
            .GroupBy(t => t.PaymentMethod ?? "other")
            .Select(g => new PaymentMethodBreakdownDto(
                g.Key,
                pmLabels.GetValueOrDefault(g.Key, g.Key),
                g.Count(),
                g.Sum(t => t.Amount),
                totalSaleAmount > 0 ? Math.Round((double)(g.Sum(t => t.Amount) / totalSaleAmount) * 100, 1) : 0
            ))
            .OrderByDescending(p => p.Amount)
            .ToList();

        return new AccountingSummaryDto(
            totalRevenue, totalRefunds, totalExpenses,
            totalRevenue - totalRefunds - totalExpenses,
            txs.Count,
            txs.Count(t => t.Status == "paid"),
            txs.Count(t => t.Status == "pending"),
            txs.Count(t => t.Status == "failed"),
            pendingTotal,
            last3MonthAvg,
            monthly, planBreakdown, pmBreakdown
        );
    }

    public async Task<List<TransactionDto>> GetTransactionsAsync(Guid tenantId, string? type, string? status, Guid? planId, DateTime? from, DateTime? to, int page, int pageSize)
    {
        var q = _db.Transactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Include(t => t.User)
            .Include(t => t.Plan)
            .AsQueryable();

        if (!string.IsNullOrEmpty(type)) q = q.Where(t => t.Type == type);
        if (!string.IsNullOrEmpty(status)) q = q.Where(t => t.Status == status);
        if (planId.HasValue) q = q.Where(t => t.PlanId == planId);
        if (from.HasValue) q = q.Where(t => t.TransactionDate >= from.Value);
        if (to.HasValue) q = q.Where(t => t.TransactionDate <= to.Value);

        return await q.OrderByDescending(t => t.TransactionDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new TransactionDto(
                t.Id, t.Type, t.Amount, t.Description, t.Status,
                t.PaymentMethod, t.InvoiceNo,
                t.UserId,
                t.User != null ? $"{t.User.FirstName} {t.User.LastName}" : null,
                t.PlanId, t.Plan != null ? t.Plan.Name : null,
                t.TransactionDate, t.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task<TransactionDto> CreateTransactionAsync(Guid tenantId, Guid userId, CreateTransactionRequest req, string? ipAddress)
    {
        var tx = new Transaction
        {
            TenantId = tenantId,
            UserId = req.UserId,
            PlanId = req.PlanId,
            Amount = req.Amount,
            Type = req.Type,
            Status = req.Status,
            PaymentMethod = req.PaymentMethod,
            Description = req.Description,
            InvoiceNo = req.InvoiceNo ?? $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            TransactionDate = req.TransactionDate ?? DateTime.UtcNow,
        };
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();
        await _db.Entry(tx).Reference(t => t.User).LoadAsync();
        await _db.Entry(tx).Reference(t => t.Plan).LoadAsync();
        await _jobQueue.EnqueueAsync(new AuditLogJob(tenantId, userId, null, "Create", "Transaction", tx.Id.ToString(), tx.Description, $"Tutar: {tx.Amount} {tx.Type}", ipAddress));
        return new TransactionDto(tx.Id, tx.Type, tx.Amount, tx.Description, tx.Status,
            tx.PaymentMethod, tx.InvoiceNo, tx.UserId,
            tx.User != null ? $"{tx.User.FirstName} {tx.User.LastName}" : null,
            tx.PlanId, tx.Plan?.Name, tx.TransactionDate, tx.CreatedAt);
    }

    public async Task<bool> DeleteTransactionAsync(Guid tenantId, Guid userId, Guid transactionId, string? ipAddress)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId && t.TenantId == tenantId);
        if (tx is null) return false;
        _db.Transactions.Remove(tx);
        await _db.SaveChangesAsync();
        await _jobQueue.EnqueueAsync(new AuditLogJob(tenantId, userId, null, "Delete", "Transaction", transactionId.ToString(), null, null, ipAddress));
        return true;
    }

    public async Task<List<PlanDto>> GetPlansAsync(Guid tenantId)
    {
        return await _db.Plans.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new PlanDto(
                p.Id, p.Name, p.Description, p.Price, p.Currency, p.BillingCycle,
                p.MaxStudents, p.IsActive,
                _db.Transactions.Count(t => t.PlanId == p.Id && t.Type == "sale" && t.Status == "paid")
            ))
            .ToListAsync();
    }

    public async Task<PlanDto> CreatePlanAsync(Guid tenantId, Guid userId, CreatePlanRequest req, string? ipAddress)
    {
        var plan = new Plan
        {
            TenantId = tenantId,
            Name = req.Name,
            Description = req.Description,
            Price = req.Price,
            Currency = req.Currency,
            BillingCycle = req.BillingCycle,
            MaxStudents = req.MaxStudents,
        };
        _db.Plans.Add(plan);
        await _db.SaveChangesAsync();
        await _jobQueue.EnqueueAsync(new AuditLogJob(tenantId, userId, null, "Create", "Plan", plan.Id.ToString(), req.Name, $"Fiyat: {req.Price}", ipAddress));
        return new PlanDto(plan.Id, plan.Name, plan.Description, plan.Price, plan.Currency, plan.BillingCycle, plan.MaxStudents, plan.IsActive, 0);
    }

    public async Task<bool> DeletePlanAsync(Guid tenantId, Guid userId, Guid planId, string? ipAddress)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.TenantId == tenantId);
        if (plan is null) return false;
        _db.Plans.Remove(plan);
        await _db.SaveChangesAsync();
        await _jobQueue.EnqueueAsync(new AuditLogJob(tenantId, userId, null, "Delete", "Plan", planId.ToString(), plan.Name, null, ipAddress));
        return true;
    }
}
