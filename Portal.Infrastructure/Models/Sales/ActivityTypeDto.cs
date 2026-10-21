namespace Portal.Infrastructure.Models.Sales;

/// <summary>A prospect-activity type for dropdowns / filters (from [sales].[ActivityType]).</summary>
public class ActivityTypeDto
{
    public byte Id { get; set; }
    public string Name { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
