namespace Portal.Infrastructure.Models.CashFlow;

public class CashFlowSettingsDto
{
    public decimal StartingBalance { get; set; }
    public decimal AlertThreshold { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
