namespace Portal.Infrastructure.Models.CashFlow;

public class InflowItemDto
{
    public int InvoiceId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string InvoiceNumber { get; set; } = null!;
    public decimal OutstandingAmount { get; set; }
    public DateOnly OriginalDueDate { get; set; }
    public DateOnly AdjustedDueDate { get; set; }
    public int DaysLateAverage { get; set; }
}
