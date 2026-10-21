namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Reference table of prospect-activity types (Research, Call, Email, Meeting, Demo, Note, Social,
/// Other, and any added later). A static lookup: new types are added by inserting rows, which
/// surfaces them in the dropdown, filters and names with no code change.
///
/// Schema: [sales].[ActivityType]. The dependent column is [sales].[ProspectActivity].[ActivityTypeId].
/// Exempt from the CreatedAtUtc audit rule (static seed/reference data).
/// </summary>
public class ActivityType
{
    public byte Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Display order for the dropdown / filters (ascending).</summary>
    public int SortOrder { get; set; }

    /// <summary>Inactive types are hidden from new-entry pickers but still resolve for history.</summary>
    public bool IsActive { get; set; }
}
