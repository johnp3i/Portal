namespace Portal.Infrastructure.Models;

public class CheckLimitsRequest
{
    public int ExpenseCategoryId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public int? PurchaseId { get; set; } // null for create, set for edit
}
