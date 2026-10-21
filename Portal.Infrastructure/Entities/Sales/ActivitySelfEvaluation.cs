namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A private, author-only self-evaluation ("Call Reflection") attached to a single
/// [sales].[ProspectActivity]. This is a personal-learning record kept entirely separate from the
/// commercial activity: it never changes the prospect status, qualification/ICP score, interaction
/// signal, or lead conversion, and it is only visible to the user who wrote it (<see cref="EvaluatorUserId"/>).
///
/// Each of the six criteria is an optional 1-5 rating; NULL means "not assessed" and is distinct
/// from a score of 1. The self-assessment average is computed in application logic from the rated
/// criteria only, so it is not persisted.
///
/// Schema: [sales].[ActivitySelfEvaluation]. Unique per (BusinessId, ProspectActivityId, EvaluatorUserId).
/// </summary>
public class ActivitySelfEvaluation
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int ProspectActivityId { get; set; }

    /// <summary>The author of the reflection (ASP.NET Identity user id). Author-only privacy is
    /// enforced against this column, independent of the activity's own PerformedByUserId.</summary>
    public string EvaluatorUserId { get; set; } = null!;

    /// <summary>Opening &amp; credibility — 1-5, or NULL = not assessed.</summary>
    public byte? OpeningCredibility { get; set; }

    /// <summary>Listening &amp; discovery — 1-5, or NULL = not assessed.</summary>
    public byte? ListeningDiscovery { get; set; }

    /// <summary>Adaptability — 1-5, or NULL = not assessed.</summary>
    public byte? Adaptability { get; set; }

    /// <summary>Clarity &amp; relevance — 1-5, or NULL = not assessed.</summary>
    public byte? ClarityRelevance { get; set; }

    /// <summary>Conversation control — 1-5, or NULL = not assessed.</summary>
    public byte? ConversationControl { get; set; }

    /// <summary>Next-step agreement — 1-5, or NULL = not assessed.</summary>
    public byte? NextStepAgreement { get; set; }

    /// <summary>"What did I notice about this conversation?"</summary>
    public string? Observations { get; set; }

    /// <summary>"What did I do effectively?"</summary>
    public string? Strengths { get; set; }

    /// <summary>"What will I do differently next time?" (one concrete behaviour).</summary>
    public string? NextImprovement { get; set; }

    /// <summary>Optional pre-call commercial objective, recorded without fabricating a completed call.</summary>
    public string? PreCallObjective { get; set; }

    /// <summary>Optional pre-call personal practice focus.</summary>
    public string? PreCallFocus { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ProspectActivity ProspectActivity { get; set; } = null!;
}
