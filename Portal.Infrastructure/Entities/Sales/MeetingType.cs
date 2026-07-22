namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Lookup: defines the format of a scheduled meeting (Online, On-Site, Phone Call, etc.).
/// Schema: [sales].[MeetingType]
/// </summary>
public class MeetingType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
