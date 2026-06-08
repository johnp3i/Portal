namespace Portal.Infrastructure.Entities.Billing;

/// <summary>
/// Tracks the last assigned invoice sequence number per calendar year.
/// Schema: [billing].InvoiceSequence
/// </summary>
public class InvoiceSequence
{
    public int Year { get; set; }

    public int LastNumber { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
