namespace Portal.Infrastructure.Entities;

/// <summary>
/// Represents a compliance filing type template (e.g., VAT Return, Annual Levy).
/// Schema: [compliance].ApplicationType
/// </summary>
public class ApplicationType
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Country { get; set; } = string.Empty;

    public int ApplicationCategoryId { get; set; }

    public string Frequency { get; set; } = string.Empty;

    public int? DefaultDueMonth { get; set; }

    public int? DefaultDueDay { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
