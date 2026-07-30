namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// DTO for application type templates (admin and import views).
/// </summary>
public class ApplicationTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Country { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int ApplicationCategoryId { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public int? DefaultDueMonth { get; set; }
    public int? DefaultDueDay { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public int? FrequencyInterval { get; set; }
    public bool IsActive { get; set; }
}
