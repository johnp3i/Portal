namespace Portal.Infrastructure.Models;

public class LimitWarning
{
    public string LimitType { get; set; } = null!;  // "annual" or "period"
    public decimal ConfiguredLimit { get; set; }
    public decimal CumulativeTotal { get; set; }
    public decimal ProjectedTotal { get; set; }
    public decimal ExceededBy { get; set; }
}
