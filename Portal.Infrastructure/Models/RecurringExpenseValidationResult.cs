namespace Portal.Infrastructure.Models;

public class RecurringExpenseValidationResult
{
    public ValidationSummary Summary { get; set; } = new();
    public List<RuleValidationResult> RuleResults { get; set; } = new();
}
