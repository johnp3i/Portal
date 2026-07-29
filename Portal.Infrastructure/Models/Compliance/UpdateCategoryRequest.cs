namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// Request model for updating an existing application category (admin).
/// </summary>
public class UpdateCategoryRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
