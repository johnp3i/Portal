namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// An immutable record of an action performed on a lead.
/// Schema: [sales].ActivityFeed
/// </summary>
public class ActivityFeedEntry
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int LeadRequestId { get; set; }
    public string Action { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? PerformedByUserId { get; set; }
    public int? PerformedByTeamMemberId { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public Business Business { get; set; } = null!;
    public LeadRequest LeadRequest { get; set; } = null!;
    public TeamMember? PerformedByTeamMember { get; set; }
}
