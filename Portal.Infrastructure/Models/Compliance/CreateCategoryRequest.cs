namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// Request model for creating a new application category (admin).
/// </summary>
public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
