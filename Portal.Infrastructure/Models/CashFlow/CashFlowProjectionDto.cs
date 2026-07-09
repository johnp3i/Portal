namespace Portal.Infrastructure.Models.CashFlow;

public class CashFlowProjectionDto
{
    public decimal StartingBalance { get; set; }
    public decimal AlertThreshold { get; set; }
    public decimal TotalInflows { get; set; }
    public decimal TotalOutflows { get; set; }
    public decimal ProjectedBalance { get; set; }
    public List<DailyBalanceDto> DailyBalances { get; set; } = new();
    public List<InflowItemDto> Inflows { get; set; } = new();
    public List<OutflowCategoryDto> Outflows { get; set; } = new();
    public DateTime? AlertBreachDate { get; set; }
}
