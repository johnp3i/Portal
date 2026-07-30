namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// DTO for the dashboard upcoming filings widget.
/// </summary>
public class UpcomingFilingDto
{
    public int Id { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? EstimatedAmount { get; set; }
    public string DueStatus { get; set; } = string.Empty;
    public int? DaysUntilDue { get; set; }
}
