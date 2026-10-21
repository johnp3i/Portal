namespace Portal.Infrastructure.Models.Sales;

// ============================================================================
// Call Reflection / Self-Evaluation DTOs
//
// A PRIVATE, author-only personal-learning record attached to a prospect activity. Kept entirely
// separate from the commercial activity: it never changes prospect status, qualification/ICP score,
// interaction signal, or lead conversion.
//
// Each criterion is an optional 1-5 rating; null = "not assessed" (distinct from a score of 1).
// The self-assessment average is computed from the rated criteria only — never stored.
// ============================================================================

/// <summary>
/// The six self-assessment criteria. Shared between the DTO and the request so the field set stays
/// in one place. Each is null (not assessed) or an integer 1-5.
/// </summary>
public static class SelfEvaluationCriteria
{
    public const int Count = 6;
}

/// <summary>A self-evaluation returned to its author, with the computed average.</summary>
public class SelfEvaluationDto
{
    public int Id { get; set; }
    public int ProspectActivityId { get; set; }

    public byte? OpeningCredibility { get; set; }
    public byte? ListeningDiscovery { get; set; }
    public byte? Adaptability { get; set; }
    public byte? ClarityRelevance { get; set; }
    public byte? ConversationControl { get; set; }
    public byte? NextStepAgreement { get; set; }

    public string? Observations { get; set; }
    public string? Strengths { get; set; }
    public string? NextImprovement { get; set; }

    public string? PreCallObjective { get; set; }
    public string? PreCallFocus { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Number of criteria rated (0-6). The denominator for the average and the assessed count.</summary>
    public int AssessedCount { get; set; }

    /// <summary>Arithmetic mean of the rated criteria only, one decimal, or null when none rated
    /// ("Not scored"). Never derived from 0 when nothing is assessed.</summary>
    public decimal? Average { get; set; }

    /// <summary>Convenience display: e.g. "4.0 / 5 · 5 of 6 assessed", or "Not scored".</summary>
    public string AverageDisplay { get; set; } = "Not scored";
}

/// <summary>
/// Create-or-update request for the author's reflection on one activity. All ratings and notes are
/// optional — a notes-only or partial reflection is valid, and no rating is mandatory to save.
/// </summary>
public class SaveSelfEvaluationRequest
{
    public int ProspectActivityId { get; set; }

    public byte? OpeningCredibility { get; set; }
    public byte? ListeningDiscovery { get; set; }
    public byte? Adaptability { get; set; }
    public byte? ClarityRelevance { get; set; }
    public byte? ConversationControl { get; set; }
    public byte? NextStepAgreement { get; set; }

    public string? Observations { get; set; }
    public string? Strengths { get; set; }
    public string? NextImprovement { get; set; }

    public string? PreCallObjective { get; set; }
    public string? PreCallFocus { get; set; }
}

/// <summary>Delete the author's reflection for one activity (JSON body so it carries the antiforgery token).</summary>
public class DeleteSelfEvaluationRequest
{
    public int ProspectActivityId { get; set; }
}

// ============================================================================
// My Development dashboard DTOs (private, author-only, self-reported)
// ============================================================================

/// <summary>Average of one criterion over the period, with the sample size it was rated on.</summary>
public class CriterionAverageDto
{
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    /// <summary>Mean of rated values (1 decimal), or null when the criterion was never rated.</summary>
    public decimal? Average { get; set; }
    /// <summary>How many evaluations rated this criterion (the "n").</summary>
    public int SampleSize { get; set; }
}

/// <summary>A recent improvement note ("what I'll do differently"), newest first.</summary>
public class RecentLessonDto
{
    public int ProspectActivityId { get; set; }
    public string Text { get; set; } = null!;
    public DateTime DateUtc { get; set; }
}

/// <summary>An eligible activity (Call/Meeting/Demo) with no reflection yet — the review queue.</summary>
public class ReviewQueueItemDto
{
    public int ProspectActivityId { get; set; }
    public int ProspectId { get; set; }
    public string ProspectName { get; set; } = null!;
    public byte ActivityType { get; set; }
    public string ActivityTypeName { get; set; } = null!;
    public string? Outcome { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

/// <summary>The private My Development dashboard for one user + business + period.</summary>
public class MyDevelopmentDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }

    /// <summary>Reflections completed in the period.</summary>
    public int EvaluationsCompleted { get; set; }

    /// <summary>Overall average across all rated criteria in the period, or null ("—").</summary>
    public decimal? OverallAverage { get; set; }
    /// <summary>Total rated criterion-observations behind the overall average.</summary>
    public int OverallAssessedCount { get; set; }

    /// <summary>Eligible Call/Meeting/Demo activities in the period, and how many were reflected on.</summary>
    public int EligibleActivities { get; set; }
    public int ReflectedActivities { get; set; }

    public List<CriterionAverageDto> CriterionAverages { get; set; } = new();
    public List<RecentLessonDto> RecentLessons { get; set; } = new();
    public List<ReviewQueueItemDto> ReviewQueue { get; set; } = new();

    /// <summary>The user's current standing practice focus, or null if none set.</summary>
    public string? WeeklyFocus { get; set; }
}

/// <summary>Set/clear the user's standing practice focus (JSON body carries the antiforgery token).</summary>
public class SavePracticeFocusRequest
{
    public string? Focus { get; set; }
}
