namespace Portal.Web.Models;

public class ClearLimitRequest
{
    public int ExpenseCategoryId { get; set; }
    public string LimitType { get; set; } = null!;  // "annual" or "period"
}
