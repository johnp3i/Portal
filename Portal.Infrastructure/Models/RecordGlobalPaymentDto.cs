namespace Portal.Infrastructure.Models;

/// <summary>
/// Input DTO for recording a global (customer-level) payment with automatic or manual allocation.
/// </summary>
public class RecordGlobalPaymentDto
{
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public int PaymentMethodTypeId { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Allocation strategy: "fifo" (default) or "manual".
    /// </summary>
    public string AllocationMode { get; set; } = "fifo";

    /// <summary>
    /// Required when AllocationMode = "manual". Each item specifies an invoice and amount to allocate.
    /// </summary>
    public List<ManualAllocationItem>? ManualAllocations { get; set; }
}

/// <summary>
/// A single manual allocation entry: which invoice receives what portion of the payment.
/// </summary>
public class ManualAllocationItem
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
}
