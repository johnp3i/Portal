namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A prospect's own activity timeline entry (call, email, note, etc.). Separate from the
/// lead-keyed [sales].[ActivityFeed] because prospects exist before any lead. Recording an
/// activity never creates a Lead or Sales Contact, and activities survive conversion.
/// Schema: [sales].[ProspectActivity]
/// </summary>
public class ProspectActivity
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int ProspectId { get; set; }

    /// <summary>
    /// FK to [sales].[ActivityType].[Id] (1 Research, 2 Call, 3 Email, 4 Meeting, 5 Demo, 6 Note,
    /// 7 Social, 8 Other, plus any added later). Mapped to the DB column [ActivityTypeId]; the
    /// property keeps the shorter name used across the codebase.
    /// </summary>
    public byte ActivityType { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string? PerformedByUserId { get; set; }

    /// <summary>Optional interaction signal: 1 Positive, 2 Neutral, 3 Negative.</summary>
    public byte? Sentiment { get; set; }

    /// <summary>Flags an entry that needs a follow-up (drives the campaign's follow-ups-due count).</summary>
    public bool IsFollowUp { get; set; }

    public string? Outcome { get; set; }

    public string? Notes { get; set; }

    public string? NextAction { get; set; }

    public DateOnly? NextActionDate { get; set; }

    /// <summary>Optional time-of-day for the next action (e.g. 10:00). NULL = no specific time.</summary>
    public TimeOnly? NextActionTime { get; set; }

    /// <summary>Optional contact name captured for the follow-up (e.g. the person who called back).</summary>
    public string? NextActionContactName { get; set; }

    /// <summary>Optional phone number to use for the follow-up (e.g. the mobile they called from).</summary>
    public string? NextActionPhone { get; set; }

    /// <summary>Optional email address to use for the follow-up (e.g. the contact's email).</summary>
    public string? NextActionEmail { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Prospect Prospect { get; set; } = null!;
    // No ActivityType reference navigation: the FK is configured in PortalDbContext without one, and
    // type names are resolved through the cached IActivityTypeLookupService rather than EF Include.
}
