namespace Portal.Infrastructure.Models;

/// <summary>
/// Filter parameters for the Business Insights admin page.
/// </summary>
public class BusinessInsightFilter
{
    public string? SearchTerm { get; set; }
    public string? PlanFilter { get; set; }
    public string? StatusFilter { get; set; }
    public string? ActivityFilter { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
