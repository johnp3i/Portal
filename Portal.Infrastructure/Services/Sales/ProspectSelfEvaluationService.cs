using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for the private Call Reflection / self-evaluation layer. Enforces tenant isolation
/// (<see cref="ICurrentTenantService"/>) and author-only access (the caller-supplied evaluator id),
/// validates ratings and note lengths, and computes the self-assessment average from rated criteria
/// only. Never mutates the commercial activity or any pipeline/scoring data.
/// </summary>
public class ProspectSelfEvaluationService : IProspectSelfEvaluationService
{
    private readonly ActivitySelfEvaluationRepository _repository;
    private readonly ProspectActivityRepository _activityRepository;
    private readonly UserPracticeFocusRepository _practiceFocusRepository;
    private readonly IActivityTypeLookupService _activityTypeLookup;
    private readonly ICurrentTenantService _tenantService;

    // Note length cap, mirrors the DB column widths (see migration 226 / EF config).
    private const int MaxNoteLength = 2000;
    private const int MaxPreCallLength = 1000;

    // Caps for dashboard lists so a long history stays responsive.
    private const int RecentLessonsCap = 8;
    private const int ReviewQueueCap = 10;
    // Upper bound on eligible activities fetched for the reflected-count + review queue. Generous for
    // a personal prospecting tool; the accurate total uses an unbounded COUNT regardless.
    private const int EligibleFetchCap = 500;

    // The six criteria, in display order: [key, label, entity accessor].
    private static readonly (string Key, string Label, Func<Entities.Sales.ActivitySelfEvaluation, byte?> Get)[] CriteriaMap =
    {
        ("openingCredibility",  "Opening & credibility",  e => e.OpeningCredibility),
        ("listeningDiscovery",  "Listening & discovery",  e => e.ListeningDiscovery),
        ("adaptability",        "Adaptability",           e => e.Adaptability),
        ("clarityRelevance",    "Clarity & relevance",    e => e.ClarityRelevance),
        ("conversationControl", "Conversation control",   e => e.ConversationControl),
        ("nextStepAgreement",   "Next-step agreement",    e => e.NextStepAgreement),
    };

    public ProspectSelfEvaluationService(
        ActivitySelfEvaluationRepository repository,
        ProspectActivityRepository activityRepository,
        UserPracticeFocusRepository practiceFocusRepository,
        IActivityTypeLookupService activityTypeLookup,
        ICurrentTenantService tenantService)
    {
        _repository = repository;
        _activityRepository = activityRepository;
        _practiceFocusRepository = practiceFocusRepository;
        _activityTypeLookup = activityTypeLookup;
        _tenantService = tenantService;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SelfEvaluationDto?>> GetForActivityAsync(int prospectActivityId, string evaluatorUserId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(evaluatorUserId))
                return ServiceResult<SelfEvaluationDto?>.Fail("Not authenticated.");

            var businessId = _tenantService.CurrentBusinessId;

            // The activity must exist within the caller's tenant; otherwise there is nothing to reflect on.
            var activity = await _activityRepository.GetByIdAsync(prospectActivityId, businessId);
            if (activity == null)
                return ServiceResult<SelfEvaluationDto?>.Fail("Activity not found.");

            var entity = await _repository.GetByActivityAsync(prospectActivityId, businessId, evaluatorUserId);
            return ServiceResult<SelfEvaluationDto?>.Ok(entity == null ? null : MapToDto(entity));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SelfEvaluationDto>> SaveAsync(SaveSelfEvaluationRequest request, string evaluatorUserId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(evaluatorUserId))
                return ServiceResult<SelfEvaluationDto>.Fail("Not authenticated.");

            var businessId = _tenantService.CurrentBusinessId;

            var activity = await _activityRepository.GetByIdAsync(request.ProspectActivityId, businessId);
            if (activity == null)
                return ServiceResult<SelfEvaluationDto>.Fail("Activity not found.");

            // Validate every rating is null or an integer 1-5 (defence in depth over the DB CHECK).
            if (!IsValidRating(request.OpeningCredibility)
                || !IsValidRating(request.ListeningDiscovery)
                || !IsValidRating(request.Adaptability)
                || !IsValidRating(request.ClarityRelevance)
                || !IsValidRating(request.ConversationControl)
                || !IsValidRating(request.NextStepAgreement))
            {
                return ServiceResult<SelfEvaluationDto>.Fail("Each rating must be between 1 and 5, or left unassessed.");
            }

            var entity = new ActivitySelfEvaluation
            {
                BusinessId = businessId,
                ProspectActivityId = request.ProspectActivityId,
                EvaluatorUserId = evaluatorUserId,
                OpeningCredibility = request.OpeningCredibility,
                ListeningDiscovery = request.ListeningDiscovery,
                Adaptability = request.Adaptability,
                ClarityRelevance = request.ClarityRelevance,
                ConversationControl = request.ConversationControl,
                NextStepAgreement = request.NextStepAgreement,
                Observations = Clean(request.Observations, MaxNoteLength),
                Strengths = Clean(request.Strengths, MaxNoteLength),
                NextImprovement = Clean(request.NextImprovement, MaxNoteLength),
                PreCallObjective = Clean(request.PreCallObjective, MaxPreCallLength),
                PreCallFocus = Clean(request.PreCallFocus, MaxPreCallLength)
            };

            await _repository.UpsertAsync(entity);

            // Re-read so the returned DTO carries the authoritative persisted row (id, timestamps).
            var saved = await _repository.GetByActivityAsync(request.ProspectActivityId, businessId, evaluatorUserId);
            if (saved == null)
                return ServiceResult<SelfEvaluationDto>.Fail("Could not save the reflection.");

            return ServiceResult<SelfEvaluationDto>.Ok(MapToDto(saved));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(int prospectActivityId, string evaluatorUserId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(evaluatorUserId))
                return ServiceResult.Fail("Not authenticated.");

            var businessId = _tenantService.CurrentBusinessId;
            await _repository.DeleteAsync(prospectActivityId, businessId, evaluatorUserId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<HashSet<int>> GetEvaluatedActivityIdsAsync(IEnumerable<int> prospectActivityIds, string evaluatorUserId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(evaluatorUserId))
                return new HashSet<int>();

            var businessId = _tenantService.CurrentBusinessId;
            return await _repository.GetEvaluatedActivityIdsAsync(prospectActivityIds, businessId, evaluatorUserId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<MyDevelopmentDto> GetMyDevelopmentAsync(string evaluatorUserId, DateTime fromUtc, DateTime toUtc)
    {
        try
        {
            var dto = new MyDevelopmentDto { FromUtc = fromUtc, ToUtc = toUtc };
            if (string.IsNullOrWhiteSpace(evaluatorUserId))
                return dto;

            var businessId = _tenantService.CurrentBusinessId;

            // All the author's reflections in the window (author + tenant scoped in the repo).
            var reflections = await _repository.GetForDashboardAsync(businessId, evaluatorUserId, fromUtc, toUtc);

            dto.EvaluationsCompleted = reflections.Count;

            // Per-criterion averages + sample sizes, and the overall average across every rated value.
            var allRated = new List<int>();
            foreach (var (key, label, get) in CriteriaMap)
            {
                var rated = reflections
                    .Select(get)
                    .Where(v => v.HasValue && v.Value >= 1 && v.Value <= 5)
                    .Select(v => (int)v!.Value)
                    .ToList();

                allRated.AddRange(rated);

                dto.CriterionAverages.Add(new CriterionAverageDto
                {
                    Key = key,
                    Label = label,
                    SampleSize = rated.Count,
                    Average = rated.Count > 0
                        ? Math.Round((decimal)rated.Sum() / rated.Count, 1, MidpointRounding.AwayFromZero)
                        : (decimal?)null
                });
            }

            dto.OverallAssessedCount = allRated.Count;
            dto.OverallAverage = allRated.Count > 0
                ? Math.Round((decimal)allRated.Sum() / allRated.Count, 1, MidpointRounding.AwayFromZero)
                : (decimal?)null;

            // Recent lessons — the newest non-empty "next improvement" notes.
            dto.RecentLessons = reflections
                .Where(r => !string.IsNullOrWhiteSpace(r.NextImprovement))
                .OrderByDescending(r => r.UpdatedAtUtc ?? r.CreatedAtUtc)
                .Take(RecentLessonsCap)
                .Select(r => new RecentLessonDto
                {
                    ProspectActivityId = r.ProspectActivityId,
                    Text = r.NextImprovement!.Trim(),
                    DateUtc = r.UpdatedAtUtc ?? r.CreatedAtUtc
                })
                .ToList();

            // Total eligible Call/Meeting/Demo activities in the window — accurate (unbounded COUNT),
            // so the "X of Y reflected" denominator is correct regardless of any list cap.
            dto.EligibleActivities = await _activityRepository.CountEligibleActivitiesInRangeAsync(
                businessId, fromUtc, toUtc);

            // The eligible activities themselves (bounded) — used to compute how many are reflected on
            // and to build the review queue. For a personal prospecting tool the window will not
            // exceed this cap in practice.
            var eligible = await _activityRepository.GetEligibleActivitiesInRangeAsync(
                businessId, fromUtc, toUtc, EligibleFetchCap);

            // Which eligible activities have ANY reflection by this author — determined by the activity
            // ids, NOT by the windowed reflection list, so an activity still counts as "reflected"
            // even if its reflection was written in a different period.
            var reflectedActivityIds = eligible.Count > 0
                ? await _repository.GetEvaluatedActivityIdsAsync(
                    eligible.Select(a => a.ProspectActivityId), businessId, evaluatorUserId)
                : new HashSet<int>();

            dto.ReflectedActivities = eligible.Count(a => reflectedActivityIds.Contains(a.ProspectActivityId));

            var typeNames = await _activityTypeLookup.GetNameMapAsync();
            dto.ReviewQueue = eligible
                .Where(a => !reflectedActivityIds.Contains(a.ProspectActivityId))
                .Take(ReviewQueueCap)
                .Select(a => { a.ActivityTypeName = typeNames.TryGetValue(a.ActivityType, out var tn) ? tn : "Unknown"; return a; })
                .ToList();

            // Standing practice focus (author + tenant scoped).
            var focus = await _practiceFocusRepository.GetAsync(businessId, evaluatorUserId);
            dto.WeeklyFocus = focus?.Focus;

            return dto;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SavePracticeFocusAsync(string evaluatorUserId, string? focus)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(evaluatorUserId))
                return ServiceResult.Fail("Not authenticated.");

            var businessId = _tenantService.CurrentBusinessId;
            await _practiceFocusRepository.UpsertAsync(businessId, evaluatorUserId, focus);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // --- helpers -------------------------------------------------------------

    private static bool IsValidRating(byte? value) => value is null || (value >= 1 && value <= 5);

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static SelfEvaluationDto MapToDto(ActivitySelfEvaluation e)
    {
        var ratings = new[]
        {
            e.OpeningCredibility, e.ListeningDiscovery, e.Adaptability,
            e.ClarityRelevance, e.ConversationControl, e.NextStepAgreement
        };

        // Average over RATED criteria only. None rated => null ("Not scored"), never 0.
        var rated = ratings.Where(r => r.HasValue).Select(r => (int)r!.Value).ToList();
        decimal? average = rated.Count > 0
            ? Math.Round((decimal)rated.Sum() / rated.Count, 1, MidpointRounding.AwayFromZero)
            : null;

        var display = average.HasValue
            ? $"{average.Value:0.0} / 5 · {rated.Count} of {SelfEvaluationCriteria.Count} assessed"
            : "Not scored";

        return new SelfEvaluationDto
        {
            Id = e.Id,
            ProspectActivityId = e.ProspectActivityId,
            OpeningCredibility = e.OpeningCredibility,
            ListeningDiscovery = e.ListeningDiscovery,
            Adaptability = e.Adaptability,
            ClarityRelevance = e.ClarityRelevance,
            ConversationControl = e.ConversationControl,
            NextStepAgreement = e.NextStepAgreement,
            Observations = e.Observations,
            Strengths = e.Strengths,
            NextImprovement = e.NextImprovement,
            PreCallObjective = e.PreCallObjective,
            PreCallFocus = e.PreCallFocus,
            CreatedAtUtc = e.CreatedAtUtc,
            UpdatedAtUtc = e.UpdatedAtUtc,
            AssessedCount = rated.Count,
            Average = average,
            AverageDisplay = display
        };
    }
}
