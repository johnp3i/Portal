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

    /// <summary>1 Research, 2 Call, 3 Email, 4 Meeting, 5 Demo, 6 Note, 7 Social, 8 Other.</summary>
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

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Prospect Prospect { get; set; } = null!;
}
