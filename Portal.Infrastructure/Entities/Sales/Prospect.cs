namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A researched target inside a prospecting campaign. A Prospect is NOT a Sales Contact and
/// never touches the lead pipeline until an explicit Convert-to-Lead. Phone/Email here are
/// plain research fields, not a contact record.
/// Total (0–20) and Priority (A/B/Hold) are computed from the four score dimensions — not stored.
/// Schema: [sales].[Prospect]
/// </summary>
public class Prospect
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int ProspectCampaignId { get; set; }

    public string Name { get; set; } = null!;

    public string? Segment { get; set; }

    public string? Location { get; set; }

    public string? BusinessType { get; set; }

    public string? PublicContactRole { get; set; }

    /// <summary>Research field — NOT a Sales Contact phone.</summary>
    public string? Phone { get; set; }

    /// <summary>Research field — NOT a Sales Contact email.</summary>
    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? PublicEvidence { get; set; }

    public string? WhyFit { get; set; }

    public string? ResearchSourceUrl { get; set; }

    public string? RecommendedFirstContact { get; set; }

    /// <summary>Qualification dimension, 0–5.</summary>
    public byte IcpFitScore { get; set; }

    /// <summary>Qualification dimension, 0–5.</summary>
    public byte PainProbabilityScore { get; set; }

    /// <summary>Qualification dimension, 0–5.</summary>
    public byte AccessibilityScore { get; set; }

    /// <summary>Qualification dimension, 0–5.</summary>
    public byte LearningValueScore { get; set; }

    /// <summary>1 Research, 2 Ready, 3 Contacting, 4 Engaged, 5 Converted, 6 Disqualified.</summary>
    public byte Status { get; set; }

    public string? AssignedToUserId { get; set; }

    public string? NextAction { get; set; }

    public DateOnly? NextActionDate { get; set; }

    /// <summary>
    /// Measurement link: the lead created when this prospect was converted. Kept so predicted
    /// score/priority can later be compared against actual conversion outcomes.
    /// </summary>
    public int? ConvertedLeadRequestId { get; set; }

    public DateTime? ConvertedAtUtc { get; set; }

    /// <summary>The source workbook's row identifier (e.g. "A01"), for idempotent re-import. NOT the PK.</summary>
    public string? ImportRowId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ProspectCampaign ProspectCampaign { get; set; } = null!;

    public LeadRequest? ConvertedLeadRequest { get; set; }

    public ICollection<ProspectActivity> Activities { get; set; } = new List<ProspectActivity>();
}
