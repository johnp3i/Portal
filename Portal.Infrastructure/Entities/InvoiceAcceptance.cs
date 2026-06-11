namespace Portal.Infrastructure.Entities;

/// <summary>
/// An immutable audit record capturing a customer's formal acceptance of a shared invoice.
/// Schema: [invoice].[InvoiceAcceptance]
/// </summary>
public class InvoiceAcceptance
{
    public int Id { get; set; }

    public int InvoiceShareId { get; set; }

    public string AcceptedTerms { get; set; } = null!;

    public DateTimeOffset AcceptedAtUtc { get; set; }

    public string IpAddress { get; set; } = null!;

    public string UserAgent { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    // Navigation property
    public InvoiceShare InvoiceShare { get; set; } = null!;
}
