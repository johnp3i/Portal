namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// DTO for the compliance filings list view.
/// </summary>
public class BusinessApplicationDto
{
    public int Id { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public int AttachmentCount { get; set; }
    public string DueStatus { get; set; } = string.Empty;
    public int? DaysUntilDue { get; set; }
}
