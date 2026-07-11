namespace Portal.Web.Models;

public class AssignPurchasesRequest
{
    public int PeriodId { get; set; }
    public List<int> PurchaseIds { get; set; } = new();
}
