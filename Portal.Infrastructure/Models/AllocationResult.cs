namespace Portal.Infrastructure.Models;

/// <summary>
/// The result of a global payment allocation operation, containing all child allocations
/// and the remaining credit amount (if overpayment).
/// </summary>
public class AllocationResult
{
    public List<AllocationDetail> Allocations { get; set; } = new();
    public decimal CreditAmount { get; set; }
    public int AllocatedCount => Allocations.Count;
    public decimal TotalAllocated => Allocations.Sum(a => a.AllocatedAmount);
}

/// <summary>
/// Detail of a single child allocation created during global payment distribution.
/// </summary>
public class AllocationDetail
{
    public int ChildPaymentId { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal AllocatedAmount { get; set; }
    public decimal InvoiceOutstandingAfter { get; set; }
}
