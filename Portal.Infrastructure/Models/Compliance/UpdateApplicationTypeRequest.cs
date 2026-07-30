namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// Request model for updating an existing application type template (admin).
/// </summary>
public class UpdateApplicationTypeRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Country { get; set; } = string.Empty;
    public int ApplicationCategoryId { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public int? DefaultDueMonth { get; set; }
    public int? DefaultDueDay { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public int? FrequencyInterval { get; set; }
}
