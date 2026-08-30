namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Lookup table for structured meeting outcome classifications.
/// Schema: [sales].[MeetingOutcomeClassification]
/// </summary>
public class MeetingOutcomeClassification
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}
