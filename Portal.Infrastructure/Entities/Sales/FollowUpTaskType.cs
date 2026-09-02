namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Lookup: category of a follow-up task (Call, Email, Follow-up, Meeting Prep, Other).
/// Schema: [sales].[FollowUpTaskTypes]. Mirrors [sales].[MeetingType], with a TINYINT Id.
/// </summary>
public class FollowUpTaskType
{
    public byte Id { get; set; }

    public string Name { get; set; } = null!;
}
