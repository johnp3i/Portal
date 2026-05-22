using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Models;

/// <summary>
/// DTO used as the view model for rendering the invoice snapshot Razor view.
/// Contains all data needed to produce a self-contained HTML snapshot of an invoice.
/// </summary>
public class InvoiceSnapshotModel
{
    public Invoice Invoice { get; set; } = null!;

    public List<InvoiceLine> Lines { get; set; } = new();

    public List<InvoiceSection> Sections { get; set; } = new();

    public string CustomerName { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public string BusinessName { get; set; } = null!;

    public BusinessProfile? Profile { get; set; }

    public List<BusinessPaymentDetail> PaymentDetails { get; set; } = new();
}
