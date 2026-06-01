namespace Portal.Web.Models.Stripe;

public class BillingInvoiceModel
{
    public int Id { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal AmountEur { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? PaidAtUtc { get; set; }
}
