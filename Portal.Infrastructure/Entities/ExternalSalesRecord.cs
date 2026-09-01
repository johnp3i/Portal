namespace Portal.Infrastructure.Entities;

/// <summary>
/// A transaction-level sales record imported from an external POS system.
/// Each record represents one individual sale/transaction (unlike RevenueSummary which is aggregated).
/// Schema: [revenue].ExternalSalesRecord
/// </summary>
public class ExternalSalesRecord
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int? RevenueSourceId { get; set; }

    public DateOnly TransactionDate { get; set; }

    public string? InvoiceNumber { get; set; }

    public int? CustomerId { get; set; }

    public decimal NetAmount { get; set; }

    public decimal VatAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Description { get; set; }

    public string? PaymentMethod { get; set; }

    public int? ImportSessionId { get; set; }

    public int? ExternalPlatformId { get; set; }

    public int? VatSubmissionPeriodId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public RevenueSource? RevenueSource { get; set; }

    public ExternalPlatform? ExternalPlatform { get; set; }

    public Customer? Customer { get; set; }

    public VatSubmissionPeriod? VatSubmissionPeriod { get; set; }
}
