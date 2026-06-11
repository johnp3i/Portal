namespace Portal.Infrastructure.Models;

/// <summary>
/// Represents the outcome of an invoice acceptance operation.
/// </summary>
public class InvoiceAcceptanceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public bool AlreadyAccepted { get; set; }
}
