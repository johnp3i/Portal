namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a receivable invoice row in the receivables list view.
/// </summary>
public class ReceivableDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }

    /// <summary>Total applied (non-voided) credit notes reducing this invoice's balance.</summary>
    public decimal TotalCredited { get; set; }

    /// <summary>TotalAmount − valid Payments − applied credit notes (authoritative balance).</summary>
    public decimal OutstandingBalance { get; set; }
    public int InvoiceFinancialStatusTypeId { get; set; }
    public string FinancialStatusName { get; set; } = null!;
    public bool HasOutstandingBalance { get; set; }

    /// <summary>
    /// Derived overdue flag: outstanding balance &gt; 0 AND due date has passed. Computed from
    /// balance + due date, NOT read from the persisted InvoiceFinancialStatusTypeId (which can be
    /// stale — an unpaid invoice that becomes overdue purely by time is not re-flagged until a
    /// payment/credit event touches it). See the derive-always overdue convention (TD-2).
    /// </summary>
    public bool IsOverdue { get; set; }
}
