namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// Validation result for custom date ranges.
/// </summary>
public class ExpenseInsightsValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}
