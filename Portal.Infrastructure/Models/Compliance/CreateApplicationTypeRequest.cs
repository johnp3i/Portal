namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// Request model for creating a new application type template (admin).
/// </summary>
public class CreateApplicationTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Country { get; set; } = string.Empty;
    public int ApplicationCategoryId { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public int? DefaultDueMonth { get; set; }
    public int? DefaultDueDay { get; set; }
}
