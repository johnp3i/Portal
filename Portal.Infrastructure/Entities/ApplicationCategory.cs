namespace Portal.Infrastructure.Entities;

/// <summary>
/// Represents a compliance application category (e.g., Tax, Employee, Regulatory).
/// Schema: [compliance].ApplicationCategory
/// </summary>
public class ApplicationCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
