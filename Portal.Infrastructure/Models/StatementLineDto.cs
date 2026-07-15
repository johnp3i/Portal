namespace Portal.Infrastructure.Models;

/// <summary>
/// Represents a single line in a customer statement of account.
/// </summary>
public class StatementLineDto
{
    public DateOnly Date { get; set; }
    public StatementLineType Type { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
    /// <summary>Payment ID for void action (null for non-payment lines).</summary>
    public int? PaymentId { get; set; }
    /// <summary>Whether this payment is voided (for display styling).</summary>
    public bool IsVoided { get; set; }
}
