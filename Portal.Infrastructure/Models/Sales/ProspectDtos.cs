namespace Portal.Infrastructure.Models.Sales;

// ============================================================================
// Prospects & Campaigns V1 DTOs
// A Prospect is NOT a Sales Contact. It lives inside a ProspectCampaign and only
// enters the lead pipeline through an explicit Convert-to-Lead.
// Total (0-20) and Priority (A/B/Hold) are COMPUTED from the four score dimensions.
// ============================================================================

// ---------------------------------------------------------------------------
// Scoring helpers (single source of truth for Total + Priority band)
// ---------------------------------------------------------------------------

/// <summary>
/// Computes the prospect Total score and Priority band from the four qualification
/// dimensions. Kept in one place so the list, detail, import and filters agree.
/// Total = ICP + Pain + Accessibility + LearningValue (each 0-5, so 0-20).
/// Band: A = 16-20, B = 12-15, Hold = &lt; 12.
/// </summary>
public static class ProspectScoring
{
    public const string BandA = "A";
    public const string BandB = "B";
    public const string BandHold = "Hold";

    public static int ComputeTotal(byte icp, byte pain, byte accessibility, byte learningValue)
        => icp + pain + accessibility + learningValue;

    public static string ComputePriority(int total)
        => total >= 16 ? BandA : total >= 12 ? BandB : BandHold;
}

// ---------------------------------------------------------------------------
// Campaign DTOs
// ---------------------------------------------------------------------------

/// <summary>A prospecting campaign row in the campaigns list.</summary>
public class ProspectCampaignListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int? SalesProductId { get; set; }
    public string? ProductName { get; set; }
    public string? Market { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int WeeklyCallTarget { get; set; }
    public byte Status { get; set; }
    public string StatusName { get; set; } = null!;
    public int ProspectCount { get; set; }
    public int ConvertedCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Request to create a prospecting campaign.</summary>
public class CreateProspectCampaignRequest
{
    public string Name { get; set; } = null!;
    public int? SalesProductId { get; set; }
    public string? Description { get; set; }
    public string? Market { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int WeeklyCallTarget { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Request to update a prospecting campaign.</summary>
public class UpdateProspectCampaignRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int? SalesProductId { get; set; }
    public string? Description { get; set; }
    public string? Market { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int WeeklyCallTarget { get; set; }
    public byte Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Campaign dashboard: header + funnel + weekly objective.</summary>
public class ProspectCampaignDashboardDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? ProductName { get; set; }
    public int? SalesProductId { get; set; }
    public string? Market { get; set; }
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int WeeklyCallTarget { get; set; }
    public byte Status { get; set; }
    public string StatusName { get; set; } = null!;
    public string? Notes { get; set; }

    public ProspectFunnelDto Funnel { get; set; } = new();
    public WeeklyObjectiveDto WeeklyObjective { get; set; } = new();

    /// <summary>Latest activity across all prospects in the campaign (newest first).</summary>
    public List<CampaignActivityFeedItemDto> RecentActivity { get; set; } = new();
}

/// <summary>
/// Funnel counts by prospect status for a campaign. Research + Ready + Contacting +
/// Engaged + Converted (+ Disqualified surfaced separately).
/// </summary>
public class ProspectFunnelDto
{
    public int Total { get; set; }
    public int Research { get; set; }
    public int Ready { get; set; }
    public int Contacting { get; set; }
    public int Engaged { get; set; }
    public int Converted { get; set; }
    public int Disqualified { get; set; }

    /// <summary>Active pipeline = everything not converted/disqualified.</summary>
    public int Active => Research + Ready + Contacting + Engaged;
}

/// <summary>
/// V1 weekly objective panel. Static placeholder in V1 — the future Prospecting
/// Assistant will populate this from real activity. See future-features backlog.
/// </summary>
public class WeeklyObjectiveDto
{
    public int WeeklyCallTarget { get; set; }
    public int FollowUpsDue { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>True once the assistant is live; false = static V1 placeholder.</summary>
    public bool IsAssistantActive { get; set; }
}

// ---------------------------------------------------------------------------
// Prospect DTOs
// ---------------------------------------------------------------------------

/// <summary>A prospect row in the campaign working list (NOT the Sales contacts list).</summary>
public class ProspectListDto
{
    public int Id { get; set; }
    public int ProspectCampaignId { get; set; }
    public string Name { get; set; } = null!;
    public string? Segment { get; set; }
    public string? Location { get; set; }
    public int Total { get; set; }
    public string Priority { get; set; } = null!;
    public byte Status { get; set; }
    public string StatusName { get; set; } = null!;
    public string? AssignedToUserId { get; set; }
    public string? NextAction { get; set; }
    public DateOnly? NextActionDate { get; set; }
    public byte? LastActivityType { get; set; }
    public DateTime? LastActivityAtUtc { get; set; }
    public bool IsConverted { get; set; }
}

/// <summary>Full prospect detail: identity/research + score breakdown + timeline.</summary>
public class ProspectDetailDto
{
    public int Id { get; set; }
    public int ProspectCampaignId { get; set; }
    public string CampaignName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Segment { get; set; }
    public string? Location { get; set; }
    public string? BusinessType { get; set; }
    public string? PublicContactRole { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? PublicEvidence { get; set; }
    public string? WhyFit { get; set; }
    public string? ResearchSourceUrl { get; set; }
    public string? RecommendedFirstContact { get; set; }

    public byte IcpFitScore { get; set; }
    public byte PainProbabilityScore { get; set; }
    public byte AccessibilityScore { get; set; }
    public byte LearningValueScore { get; set; }
    public int Total { get; set; }
    public string Priority { get; set; } = null!;

    public byte Status { get; set; }
    public string StatusName { get; set; } = null!;
    public string? AssignedToUserId { get; set; }
    public string? NextAction { get; set; }
    public DateOnly? NextActionDate { get; set; }

    public bool IsConverted { get; set; }
    public int? ConvertedLeadRequestId { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<ProspectActivityDto> Activities { get; set; } = new();
}

/// <summary>Request to create a prospect inside a campaign.</summary>
public class CreateProspectRequest
{
    public int ProspectCampaignId { get; set; }
    public string Name { get; set; } = null!;
    public string? Segment { get; set; }
    public string? Location { get; set; }
    public string? BusinessType { get; set; }
    public string? PublicContactRole { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? PublicEvidence { get; set; }
    public string? WhyFit { get; set; }
    public string? ResearchSourceUrl { get; set; }
    public string? RecommendedFirstContact { get; set; }
    public byte IcpFitScore { get; set; }
    public byte PainProbabilityScore { get; set; }
    public byte AccessibilityScore { get; set; }
    public byte LearningValueScore { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? NextAction { get; set; }
    public DateOnly? NextActionDate { get; set; }
}

/// <summary>Request to update a prospect's research/scoring fields.</summary>
public class UpdateProspectRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Segment { get; set; }
    public string? Location { get; set; }
    public string? BusinessType { get; set; }
    public string? PublicContactRole { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? PublicEvidence { get; set; }
    public string? WhyFit { get; set; }
    public string? ResearchSourceUrl { get; set; }
    public string? RecommendedFirstContact { get; set; }
    public byte IcpFitScore { get; set; }
    public byte PainProbabilityScore { get; set; }
    public byte AccessibilityScore { get; set; }
    public byte LearningValueScore { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? NextAction { get; set; }
    public DateOnly? NextActionDate { get; set; }
}

// ---------------------------------------------------------------------------
// Prospect Activity DTOs
// ---------------------------------------------------------------------------

/// <summary>A timeline entry for a prospect.</summary>
public class ProspectActivityDto
{
    public int Id { get; set; }
    public byte ActivityType { get; set; }
    public string ActivityTypeName { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
    public string? PerformedByUserId { get; set; }
    /// <summary>1 Positive, 2 Neutral, 3 Negative, or null.</summary>
    public byte? Sentiment { get; set; }
    public bool IsFollowUp { get; set; }
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
    public string? NextAction { get; set; }
    public DateOnly? NextActionDate { get; set; }
}

/// <summary>Request to add an activity to a prospect. Never creates a Lead or Contact.</summary>
public class AddProspectActivityRequest
{
    public int ProspectId { get; set; }
    public byte ActivityType { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    /// <summary>Optional interaction signal: 1 Positive, 2 Neutral, 3 Negative.</summary>
    public byte? Sentiment { get; set; }
    public bool IsFollowUp { get; set; }
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
    public string? NextAction { get; set; }
    public DateOnly? NextActionDate { get; set; }
    /// <summary>Optional new status to move the prospect to (e.g. Contacting after first call).</summary>
    public byte? NewStatus { get; set; }
}

/// <summary>Request to edit an existing timeline activity.</summary>
public class UpdateProspectActivityRequest
{
    public int Id { get; set; }
    public byte ActivityType { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    /// <summary>Optional interaction signal: 1 Positive, 2 Neutral, 3 Negative.</summary>
    public byte? Sentiment { get; set; }
    public bool IsFollowUp { get; set; }
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
    public string? NextAction { get; set; }
    public DateOnly? NextActionDate { get; set; }
}

/// <summary>An entry in the campaign-wide recent activity feed (across all prospects).</summary>
public class CampaignActivityFeedItemDto
{
    public int ProspectId { get; set; }
    public string ProspectName { get; set; } = null!;
    public byte ActivityType { get; set; }
    public string ActivityTypeName { get; set; } = null!;
    public string? Outcome { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public byte? Sentiment { get; set; }
    public string? SentimentName { get; set; }
    public bool IsFollowUp { get; set; }
}

/// <summary>Request to convert a prospect to a Contact + Lead.</summary>
public class ConvertProspectRequest
{
    public int ProspectId { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Country { get; set; }
    public string? RequestText { get; set; }
}
