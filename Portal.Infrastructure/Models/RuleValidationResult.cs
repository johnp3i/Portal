namespace Portal.Infrastructure.Models;

public class RuleValidationResult
{
    public int RuleId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string Description { get; set; } = string.Empty;
    public int FrequencyMonths { get; set; }
    public int ExpectedCount { get; set; }
    public int ActualCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? ExpectedAmount { get; set; }
    public decimal? AmountTolerancePercent { get; set; }
    public int GracePeriodDays { get; set; }
    public bool? IsAmountMatched { get; set; }
    public int? AmountMatchCount { get; set; }
}
