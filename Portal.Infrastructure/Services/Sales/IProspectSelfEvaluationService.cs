using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Private, author-only Call Reflection / self-evaluation attached to prospect activities.
/// A reflection never changes commercial data (prospect status, qualification/ICP score,
/// interaction signal, lead conversion) and is only ever visible to the user who wrote it.
/// The caller supplies the authenticated <c>evaluatorUserId</c>; the tenant is resolved internally.
/// </summary>
public interface IProspectSelfEvaluationService
{
    /// <summary>
    /// Returns the author's reflection for an activity (with the computed average), or a success
    /// result with null data when none exists yet. Fails if the activity is not accessible.
    /// </summary>
    Task<ServiceResult<SelfEvaluationDto?>> GetForActivityAsync(int prospectActivityId, string evaluatorUserId);

    /// <summary>
    /// Creates or updates (idempotently) the author's reflection for an activity. Validates ratings
    /// (null or 1-5) and note lengths server-side. Returns the saved reflection with its average.
    /// </summary>
    Task<ServiceResult<SelfEvaluationDto>> SaveAsync(SaveSelfEvaluationRequest request, string evaluatorUserId);

    /// <summary>Deletes the author's reflection for an activity. Safe no-op if none exists.</summary>
    Task<ServiceResult> DeleteAsync(int prospectActivityId, string evaluatorUserId);

    /// <summary>
    /// Returns the subset of the given activity ids that already have a reflection by this author —
    /// drives the private "reflected" indicator on the prospect detail timeline without an N+1.
    /// </summary>
    Task<HashSet<int>> GetEvaluatedActivityIdsAsync(IEnumerable<int> prospectActivityIds, string evaluatorUserId);

    /// <summary>
    /// Builds the private My Development dashboard for the author over a date window: counts,
    /// per-criterion averages with sample sizes, recent lessons, the review queue, and the standing
    /// practice focus. All data is author-scoped and self-reported.
    /// </summary>
    Task<MyDevelopmentDto> GetMyDevelopmentAsync(string evaluatorUserId, DateTime fromUtc, DateTime toUtc);

    /// <summary>Sets or clears (empty = clear) the author's standing practice focus for the business.</summary>
    Task<ServiceResult> SavePracticeFocusAsync(string evaluatorUserId, string? focus);
}
