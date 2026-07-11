namespace Portal.Web.Models.RecurringExpense;

public class ValidationSummary
{
    public int TotalRules { get; set; }
    public int PassCount { get; set; }
    public int WarningCount { get; set; }
    public int FailCount { get; set; }
}
