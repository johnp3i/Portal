namespace Portal.Infrastructure.Models;

/// <summary>
/// Represents the outcome of a proposal acceptance operation.
/// </summary>
public class ProposalAcceptanceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public bool AlreadyAccepted { get; set; }
}
