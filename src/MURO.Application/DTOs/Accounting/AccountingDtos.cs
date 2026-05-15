namespace MURO.Application.DTOs.Accounting;

// ─── Plan ─────────────────────────────────────────────────────────────────────

public record PlanDto(
    Guid    Id,
    string  Name,
    string? Description,
    decimal Price,
    string  Currency,
    string  BillingCycle,
    int?    MaxStudents,
    bool    IsActive,
    int     TransactionCount
);

public record CreatePlanRequest(
    string  Name,
    string? Description,
    decimal Price,
    string  Currency,
    string  BillingCycle,
    int?    MaxStudents
);

// ─── Transaction ─────────────────────────────────────────────────────────────

public record TransactionDto(
    Guid     Id,
    string   Type,            // sale | refund | expense
    decimal  Amount,
    string?  Description,
    string   Status,          // pending | paid | failed | refunded
    string?  PaymentMethod,   // card | bank_transfer | cash | other
    string?  InvoiceNo,
    Guid?    UserId,
    string?  UserName,
    Guid?    PlanId,
    string?  PlanName,
    DateTime TransactionDate,
    DateTime CreatedAt
);

public record CreateTransactionRequest(
    string   Type,
    decimal  Amount,
    string?  Description,
    string   Status,
    string?  PaymentMethod,
    string?  InvoiceNo,
    Guid?    UserId,
    Guid?    PlanId,
    DateTime? TransactionDate
);

// ─── Dashboard Summary ────────────────────────────────────────────────────────

public record AccountingSummaryDto(
    decimal TotalRevenue,
    decimal TotalRefunds,
    decimal TotalExpenses,
    decimal NetRevenue,
    int     TotalTransactions,
    int     PaidCount,
    int     PendingCount,
    int     FailedCount,
    decimal PendingTotal,
    decimal Last3MonthAvgRevenue,
    List<MonthlyRevenueDto> MonthlyRevenue,
    List<PlanRevenueDto>    PlanBreakdown,
    List<PaymentMethodBreakdownDto> PaymentMethodBreakdown
);

public record MonthlyRevenueDto(
    int     Year,
    int     Month,
    string  MonthLabel,
    decimal Revenue,
    decimal Refunds,
    decimal Expenses
);

public record PlanRevenueDto(
    string  PlanName,
    decimal Revenue,
    int     SaleCount,
    string  Color
);

public record PaymentMethodBreakdownDto(
    string  Method,
    string  Label,
    int     Count,
    decimal Amount,
    double  Percentage
);
