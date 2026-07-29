namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// DTO for application category data.
/// </summary>
public class ApplicationCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
