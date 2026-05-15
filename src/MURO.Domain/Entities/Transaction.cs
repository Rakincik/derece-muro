namespace MURO.Domain.Entities;

public class Transaction
{
    public Guid   Id              { get; set; }
    public Guid   TenantId       { get; set; }
    public Guid?  UserId         { get; set; }
    public Guid?  PlanId         { get; set; }
    public decimal Amount        { get; set; }

    /// <summary>sale | refund | expense</summary>
    public string Type           { get; set; } = "sale";

    /// <summary>pending | paid | failed | refunded</summary>
    public string Status         { get; set; } = "paid";

    /// <summary>card | bank_transfer | cash | other</summary>
    public string? PaymentMethod { get; set; }

    public string? Description   { get; set; }
    public string? InvoiceNo     { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant  Tenant        { get; set; } = null!;
    public User?   User          { get; set; }
    public Plan?   Plan          { get; set; }
}
