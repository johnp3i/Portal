namespace Portal.Web.Models.Stripe;

public class BillingSummaryModel
{
    public decimal TotalPaid { get; set; }
    public int InvoiceCount { get; set; }
    public DateTime? LastPaymentDate { get; set; }
}
