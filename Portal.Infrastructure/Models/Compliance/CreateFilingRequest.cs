namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// Request model for manually creating a one-off compliance filing.
/// </summary>
public class CreateFilingRequest
{
    public string Name { get; set; } = string.Empty;
    public int ApplicationCategoryId { get; set; }
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
}
