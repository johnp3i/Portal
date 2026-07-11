namespace Portal.Web.Models.RecurringExpense;

public class RecurringExpenseValidationResult
{
    public ValidationSummary Summary { get; set; } = new();
    public List<RuleValidationResult> RuleResults { get; set; } = new();
}
