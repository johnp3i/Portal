namespace Portal.Infrastructure.Models.CashFlow;

public class CashFlowWidgetDto
{
    public decimal ProjectedBalance30Days { get; set; }
    public decimal NetInflow { get; set; }
    public bool HasAlertBreach { get; set; }
    public DateTime? AlertBreachDate { get; set; }
    public bool HasSettings { get; set; }
}
