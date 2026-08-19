namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// Priority type for dropdown display.
/// </summary>
public class LeadPriorityTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Colour { get; set; } = null!;
}
