namespace Portal.Infrastructure.Entities;

/// <summary>
/// Per-business configuration for the Cash Flow Forecasting module.
/// Stores the starting bank balance and alert threshold.
/// Schema: [cashflow].CashFlowSettings
/// </summary>
public class CashFlowSettings
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public decimal StartingBalance { get; set; }

    public decimal AlertThreshold { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;
}
