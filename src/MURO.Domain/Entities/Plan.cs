namespace MURO.Domain.Entities;

/// <summary>Subscription plan definition owned by a Tenant.</summary>
public class Plan
{
    public Guid   Id          { get; set; }
    public Guid   TenantId    { get; set; }
    public string Name        { get; set; } = string.Empty;   // "Aylık", "Yıllık"
    public string? Description { get; set; }
    public decimal Price      { get; set; }
    public string Currency    { get; set; } = "TRY";
    public string BillingCycle { get; set; } = "monthly";     // monthly | quarterly | yearly | onetime
    public int?   MaxStudents  { get; set; }
    public bool   IsActive    { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
