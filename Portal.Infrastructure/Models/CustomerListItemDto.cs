namespace Portal.Infrastructure.Models;

/// <summary>
/// A row in the Customer Registry list. Carries the customer's display fields plus two derived
/// signals used by the list columns/filters:
///   - CreatedFromLead: whether the customer originated from a sales lead's contact (ContactId set).
///   - Document counts: how many invoices and quotations exist for the customer, so users can spot
///     customers with no documents generated yet.
/// </summary>
public class CustomerListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? TelephoneNumber { get; set; }
    public string? City { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Back-link to the originating sales contact; non-null when created from a lead.</summary>
    public int? ContactId { get; set; }

    /// <summary>True when this customer was created from a sales lead's contact.</summary>
    public bool CreatedFromLead => ContactId.HasValue;

    public int InvoiceCount { get; set; }
    public int QuotationCount { get; set; }

    /// <summary>Combined invoices + quotations. Zero means nothing has been generated for this customer yet.</summary>
    public int DocumentCount => InvoiceCount + QuotationCount;
}
