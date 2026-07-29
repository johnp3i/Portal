namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// DTO for the calendar view filings data.
/// </summary>
public class CalendarFilingDto
{
    public int Id { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DueStatus { get; set; } = string.Empty;
}
