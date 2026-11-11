namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for invoice list display.
/// </summary>
public class InvoiceListDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string StatusName { get; set; } = null!;
    public string FinancialStatusName { get; set; } = null!;
    public int InvoiceStatusTypeId { get; set; }
    public int InvoiceFinancialStatusTypeId { get; set; }

    /// <summary>
    /// Acceptance status for the invoice's shared link.
    /// Null = no active share, "awaiting" = shared but not accepted, "accepted" = accepted by customer.
    /// </summary>
    public string? AcceptanceStatus { get; set; }

    /// <summary>
    /// True when at least one non-voided credit note has been applied to this invoice. Drives the
    /// small "CN" badge in the list and the credit marker in the Excel export.
    /// </summary>
    public bool HasCreditNote { get; set; }

    /// <summary>
    /// True when this (draft) invoice has been soft-deleted. Deleted rows remain in the list,
    /// rendered struck-through with a "Deleted" badge, so the invoice-number sequence stays
    /// visibly gap-free. They carry no financial weight and are excluded from exports/aggregates.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the invoice was soft-deleted (UTC). Null unless deleted.</summary>
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>The id of the user who deleted it (Membership db). Null unless deleted / unknown.</summary>
    public string? DeletedByUserId { get; set; }

    /// <summary>
    /// Display-only: the deleter's full name, resolved from the Membership database after paging.
    /// "Unknown" when the user id is null or not found. Not read from SQL.
    /// </summary>
    public string? DeletedByName { get; set; }

    /// <summary>
    /// Display-only: the deletion time converted to the business's local timezone and formatted
    /// for display (e.g. "04 Nov 2026 18:41"). Not read from SQL.
    /// </summary>
    public string? DeletedAtLocalDisplay { get; set; }
}
