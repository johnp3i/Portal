namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A lightweight follow-up reminder attached to a lead or contact.
/// Schema: [sales].[FollowUpTask]
/// </summary>
public class FollowUpTask
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int? LeadRequestId { get; set; }

    public int? ContactId { get; set; }

    public int? TeamMemberId { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>
    /// One of: Call, Email, Follow-up, Meeting Prep, Other
    /// </summary>
    public string TaskType { get; set; } = null!;

    public DateTime DueAtUtc { get; set; }

    public string? Notes { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public int SnoozedCount { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public LeadRequest? LeadRequest { get; set; }

    public SalesContact? Contact { get; set; }

    public TeamMember? TeamMember { get; set; }
}
