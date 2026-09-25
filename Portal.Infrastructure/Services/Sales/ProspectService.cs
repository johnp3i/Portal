using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for prospects. Handles CRUD, scoring (computed Total/Priority), status,
/// the activity timeline, and Convert-to-Lead (which reuses the existing Contact + Lead
/// services rather than duplicating their logic).
/// </summary>
public class ProspectService : IProspectService
{
    private readonly ProspectRepository _prospectRepository;
    private readonly ProspectCampaignRepository _campaignRepository;
    private readonly ProspectActivityRepository _activityRepository;
    private readonly IContactService _contactService;
    private readonly ILeadRequestService _leadRequestService;
    private readonly ICurrentTenantService _tenantService;

    // Cold Call lead source — already seeded, reused for all prospect conversions.
    private const int ColdCallLeadSourceTypeId = 4;

    // Prospect status: 1 Research, 2 Ready, 3 Contacting, 4 Engaged, 5 Converted, 6 Disqualified.
    private static readonly Dictionary<byte, string> StatusNames = new()
    {
        [1] = "Research",
        [2] = "Ready",
        [3] = "Contacting",
        [4] = "Engaged",
        [5] = "Converted",
        [6] = "Disqualified"
    };

    // Activity type: 1 Research, 2 Call, 3 Email, 4 Meeting, 5 Demo, 6 Note, 7 Social, 8 Other.
    private static readonly Dictionary<byte, string> ActivityTypeNames = new()
    {
        [1] = "Research",
        [2] = "Call",
        [3] = "Email",
        [4] = "Meeting",
        [5] = "Demo",
        [6] = "Note",
        [7] = "Social",
        [8] = "Other"
    };

    private const byte StatusConverted = 5;

    public ProspectService(
        ProspectRepository prospectRepository,
        ProspectCampaignRepository campaignRepository,
        ProspectActivityRepository activityRepository,
        IContactService contactService,
        ILeadRequestService leadRequestService,
        ICurrentTenantService tenantService)
    {
        _prospectRepository = prospectRepository;
        _campaignRepository = campaignRepository;
        _activityRepository = activityRepository;
        _contactService = contactService;
        _leadRequestService = leadRequestService;
        _tenantService = tenantService;
    }

    internal static string StatusName(byte status)
        => StatusNames.TryGetValue(status, out var name) ? name : "Unknown";

    internal static string ActivityTypeName(byte type)
        => ActivityTypeNames.TryGetValue(type, out var name) ? name : "Unknown";

    public async Task<ServiceResult> CreateProspectAsync(CreateProspectRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Prospect name is required.");

            var scoreError = ValidateScores(request.IcpFitScore, request.PainProbabilityScore, request.AccessibilityScore, request.LearningValueScore);
            if (scoreError != null)
                return ServiceResult.Fail(scoreError);

            var campaign = await _campaignRepository.GetByIdAsync(request.ProspectCampaignId, businessId);
            if (campaign == null)
                return ServiceResult.Fail("Campaign not found.");

            var entity = new Prospect
            {
                BusinessId = businessId,
                ProspectCampaignId = request.ProspectCampaignId,
                Name = request.Name.Trim(),
                Segment = request.Segment,
                Location = request.Location,
                BusinessType = request.BusinessType,
                PublicContactRole = request.PublicContactRole,
                Phone = request.Phone,
                Email = request.Email,
                Website = request.Website,
                PublicEvidence = request.PublicEvidence,
                WhyFit = request.WhyFit,
                ResearchSourceUrl = request.ResearchSourceUrl,
                RecommendedFirstContact = request.RecommendedFirstContact,
                IcpFitScore = request.IcpFitScore,
                PainProbabilityScore = request.PainProbabilityScore,
                AccessibilityScore = request.AccessibilityScore,
                LearningValueScore = request.LearningValueScore,
                Status = 1, // Research
                AssignedToUserId = request.AssignedToUserId,
                NextAction = request.NextAction,
                NextActionDate = request.NextActionDate
            };

            var id = await _prospectRepository.InsertAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateProspectAsync(UpdateProspectRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var existing = await _prospectRepository.GetByIdAsync(request.Id, businessId);
            if (existing == null)
                return ServiceResult.Fail("Prospect not found.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Prospect name is required.");

            var scoreError = ValidateScores(request.IcpFitScore, request.PainProbabilityScore, request.AccessibilityScore, request.LearningValueScore);
            if (scoreError != null)
                return ServiceResult.Fail(scoreError);

            existing.Name = request.Name.Trim();
            existing.Segment = request.Segment;
            existing.Location = request.Location;
            existing.BusinessType = request.BusinessType;
            existing.PublicContactRole = request.PublicContactRole;
            existing.Phone = request.Phone;
            existing.Email = request.Email;
            existing.Website = request.Website;
            existing.PublicEvidence = request.PublicEvidence;
            existing.WhyFit = request.WhyFit;
            existing.ResearchSourceUrl = request.ResearchSourceUrl;
            existing.RecommendedFirstContact = request.RecommendedFirstContact;
            existing.IcpFitScore = request.IcpFitScore;
            existing.PainProbabilityScore = request.PainProbabilityScore;
            existing.AccessibilityScore = request.AccessibilityScore;
            existing.LearningValueScore = request.LearningValueScore;
            existing.AssignedToUserId = request.AssignedToUserId;
            existing.NextAction = request.NextAction;
            existing.NextActionDate = request.NextActionDate;

            await _prospectRepository.UpdateAsync(existing);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateStatusAsync(int prospectId, byte status)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            if (!StatusNames.ContainsKey(status))
                return ServiceResult.Fail("Invalid prospect status.");

            var existing = await _prospectRepository.GetByIdAsync(prospectId, businessId);
            if (existing == null)
                return ServiceResult.Fail("Prospect not found.");

            // Converted is a terminal state set only through Convert-to-Lead.
            if (existing.Status == StatusConverted)
                return ServiceResult.Fail("This prospect has already been converted and cannot change status.");

            if (status == StatusConverted)
                return ServiceResult.Fail("Use Convert to Lead to mark a prospect as converted.");

            await _prospectRepository.UpdateStatusAsync(prospectId, businessId, status);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<ProspectListDto>> GetProspectsPagedAsync(
        int campaignId, string? priority, byte? status, string? segment, string? nextAction, int page, int pageSize)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var paged = await _prospectRepository.GetPagedByCampaignAsync(
                campaignId, businessId, priority, status, segment, nextAction, page, pageSize);

            var ids = paged.Items.Select(p => p.Id).ToList();
            var lastActivities = ids.Count > 0
                ? await _activityRepository.GetLastActivityByProspectIdsAsync(ids, businessId)
                : new Dictionary<int, ProspectActivity>();

            var items = paged.Items.Select(p =>
            {
                var total = ProspectScoring.ComputeTotal(p.IcpFitScore, p.PainProbabilityScore, p.AccessibilityScore, p.LearningValueScore);
                lastActivities.TryGetValue(p.Id, out var last);
                return new ProspectListDto
                {
                    Id = p.Id,
                    ProspectCampaignId = p.ProspectCampaignId,
                    Name = p.Name,
                    Segment = p.Segment,
                    Location = p.Location,
                    Total = total,
                    Priority = ProspectScoring.ComputePriority(total),
                    Status = p.Status,
                    StatusName = StatusName(p.Status),
                    AssignedToUserId = p.AssignedToUserId,
                    NextAction = p.NextAction,
                    NextActionDate = p.NextActionDate,
                    LastActivityType = last?.ActivityType,
                    LastActivityAtUtc = last?.OccurredAtUtc,
                    IsConverted = p.Status == StatusConverted
                };
            }).ToList();

            return new PagedResult<ProspectListDto>
            {
                Items = items,
                CurrentPage = paged.CurrentPage,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ProspectDetailDto?> GetProspectDetailAsync(int prospectId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var p = await _prospectRepository.GetByIdAsync(prospectId, businessId);
            if (p == null) return null;

            var campaign = await _campaignRepository.GetByIdAsync(p.ProspectCampaignId, businessId);
            var activities = await _activityRepository.GetByProspectAsync(prospectId, businessId);

            var total = ProspectScoring.ComputeTotal(p.IcpFitScore, p.PainProbabilityScore, p.AccessibilityScore, p.LearningValueScore);

            return new ProspectDetailDto
            {
                Id = p.Id,
                ProspectCampaignId = p.ProspectCampaignId,
                CampaignName = campaign?.Name ?? "Unknown",
                Name = p.Name,
                Segment = p.Segment,
                Location = p.Location,
                BusinessType = p.BusinessType,
                PublicContactRole = p.PublicContactRole,
                Phone = p.Phone,
                Email = p.Email,
                Website = p.Website,
                PublicEvidence = p.PublicEvidence,
                WhyFit = p.WhyFit,
                ResearchSourceUrl = p.ResearchSourceUrl,
                RecommendedFirstContact = p.RecommendedFirstContact,
                IcpFitScore = p.IcpFitScore,
                PainProbabilityScore = p.PainProbabilityScore,
                AccessibilityScore = p.AccessibilityScore,
                LearningValueScore = p.LearningValueScore,
                Total = total,
                Priority = ProspectScoring.ComputePriority(total),
                Status = p.Status,
                StatusName = StatusName(p.Status),
                AssignedToUserId = p.AssignedToUserId,
                NextAction = p.NextAction,
                NextActionDate = p.NextActionDate,
                IsConverted = p.Status == StatusConverted,
                ConvertedLeadRequestId = p.ConvertedLeadRequestId,
                ConvertedAtUtc = p.ConvertedAtUtc,
                CreatedAtUtc = p.CreatedAtUtc,
                Activities = activities.Select(a => new ProspectActivityDto
                {
                    Id = a.Id,
                    ActivityType = a.ActivityType,
                    ActivityTypeName = ActivityTypeName(a.ActivityType),
                    OccurredAtUtc = a.OccurredAtUtc,
                    PerformedByUserId = a.PerformedByUserId,
                    Sentiment = a.Sentiment,
                    IsFollowUp = a.IsFollowUp,
                    Outcome = a.Outcome,
                    Notes = a.Notes,
                    NextAction = a.NextAction,
                    NextActionDate = a.NextActionDate
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> AddActivityAsync(AddProspectActivityRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            if (!ActivityTypeNames.ContainsKey(request.ActivityType))
                return ServiceResult.Fail("Invalid activity type.");

            var prospect = await _prospectRepository.GetByIdAsync(request.ProspectId, businessId);
            if (prospect == null)
                return ServiceResult.Fail("Prospect not found.");

            if (!IsValidSentiment(request.Sentiment))
                return ServiceResult.Fail("Invalid interaction signal.");

            var activity = new ProspectActivity
            {
                BusinessId = businessId,
                ProspectId = request.ProspectId,
                ActivityType = request.ActivityType,
                OccurredAtUtc = request.OccurredAtUtc ?? DateTime.UtcNow,
                Sentiment = request.Sentiment,
                IsFollowUp = request.IsFollowUp,
                Outcome = request.Outcome,
                Notes = request.Notes,
                NextAction = request.NextAction,
                NextActionDate = request.NextActionDate
            };

            var id = await _activityRepository.InsertAsync(activity);

            // Optionally advance the prospect status (e.g. Research -> Contacting after a call),
            // but never to/away from the terminal Converted state here.
            if (request.NewStatus.HasValue
                && request.NewStatus.Value != StatusConverted
                && prospect.Status != StatusConverted
                && StatusNames.ContainsKey(request.NewStatus.Value)
                && request.NewStatus.Value != prospect.Status)
            {
                await _prospectRepository.UpdateStatusAsync(request.ProspectId, businessId, request.NewStatus.Value);
            }

            // Mirror the follow-up onto the prospect so the working list "Next Action" stays current.
            if (!string.IsNullOrWhiteSpace(request.NextAction) || request.NextActionDate.HasValue)
            {
                prospect.NextAction = request.NextAction;
                prospect.NextActionDate = request.NextActionDate;
                await _prospectRepository.UpdateAsync(prospect);
            }

            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateActivityAsync(UpdateProspectActivityRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            if (!ActivityTypeNames.ContainsKey(request.ActivityType))
                return ServiceResult.Fail("Invalid activity type.");

            if (!IsValidSentiment(request.Sentiment))
                return ServiceResult.Fail("Invalid interaction signal.");

            var existing = await _activityRepository.GetByIdAsync(request.Id, businessId);
            if (existing == null)
                return ServiceResult.Fail("Activity not found.");

            existing.ActivityType = request.ActivityType;
            existing.OccurredAtUtc = request.OccurredAtUtc ?? existing.OccurredAtUtc;
            existing.Sentiment = request.Sentiment;
            existing.IsFollowUp = request.IsFollowUp;
            existing.Outcome = request.Outcome;
            existing.Notes = request.Notes;
            existing.NextAction = request.NextAction;
            existing.NextActionDate = request.NextActionDate;

            await _activityRepository.UpdateActivityAsync(existing);
            return ServiceResult.Ok(existing.Id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ConvertToLeadAsync(ConvertProspectRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var prospect = await _prospectRepository.GetByIdAsync(request.ProspectId, businessId);
            if (prospect == null)
                return ServiceResult.Fail("Prospect not found.");

            // Guard double-convert.
            if (prospect.Status == StatusConverted)
                return ServiceResult.Fail("This prospect has already been converted.");

            if (string.IsNullOrWhiteSpace(request.FirstName))
                return ServiceResult.Fail("A first name is required to create the contact.");

            if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber))
                return ServiceResult.Fail("Either an email or a phone number is required to create the contact.");

            // 1) Create the Sales Contact (reuses existing dedup + validation).
            var contactResult = await _contactService.CreateContactAsync(new CreateContactRequest
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                CompanyName = request.CompanyName ?? prospect.Name,
                JobTitle = request.JobTitle ?? prospect.PublicContactRole,
                Country = request.Country ?? prospect.Location,
                Notes = prospect.WhyFit
            });

            if (!contactResult.Success || !contactResult.Id.HasValue)
                return ServiceResult.Fail(contactResult.Message ?? "Could not create the contact from this prospect.");

            var contactId = contactResult.Id.Value;

            // 2) Create the Lead (Source = Cold Call), product from the campaign.
            var campaign = await _campaignRepository.GetByIdAsync(prospect.ProspectCampaignId, businessId);

            var leadResult = await _leadRequestService.CreateLeadRequestAsync(new CreateLeadRequestDto
            {
                ContactId = contactId,
                ProductId = campaign?.SalesProductId,
                LeadSourceTypeId = ColdCallLeadSourceTypeId,
                SourceUrl = prospect.ResearchSourceUrl,
                RequestText = string.IsNullOrWhiteSpace(request.RequestText) ? prospect.WhyFit : request.RequestText
            });

            if (!leadResult.Success || !leadResult.Id.HasValue)
                return ServiceResult.Fail(leadResult.Message ?? "Contact created, but the lead could not be created.");

            var leadRequestId = leadResult.Id.Value;

            // 3) Mark the prospect converted and keep the back-reference for scoring measurement.
            await _prospectRepository.SetConvertedAsync(request.ProspectId, businessId, leadRequestId);

            // 4) Record the conversion on the prospect's own timeline (survives conversion).
            await _activityRepository.InsertAsync(new ProspectActivity
            {
                BusinessId = businessId,
                ProspectId = request.ProspectId,
                ActivityType = 8, // Other
                OccurredAtUtc = DateTime.UtcNow,
                Outcome = "Converted to lead",
                Notes = $"Converted to Contact #{contactId} and Lead #{leadRequestId} (Source: Cold Call)."
            });

            return ServiceResult.Ok(leadRequestId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Sentiment must be null or 1 (Positive), 2 (Neutral), 3 (Negative).</summary>
    private static bool IsValidSentiment(byte? sentiment)
        => !sentiment.HasValue || sentiment.Value is 1 or 2 or 3;

    /// <summary>Each score dimension must be 0-5. Returns an error message or null when valid.</summary>
    private static string? ValidateScores(byte icp, byte pain, byte accessibility, byte learningValue)
    {
        if (icp > 5 || pain > 5 || accessibility > 5 || learningValue > 5)
            return "Each score must be between 0 and 5.";
        return null;
    }
}
