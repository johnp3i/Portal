namespace Portal.Web.Models;

public class UnassignPurchasesRequest
{
    public List<int> PurchaseIds { get; set; } = new();
}
