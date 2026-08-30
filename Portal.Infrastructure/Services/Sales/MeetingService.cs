using System.Text;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for meeting management.
/// </summary>
public class MeetingService : IMeetingService
{
    private readonly MeetingRepository _meetingRepository;
    private readonly MeetingProductRequestRepository _productRequestRepository;
    private readonly MeetingOpportunityRepository _opportunityRepository;
    private readonly SalesContactRepository _contactRepository;
    private readonly SalesProductRepository _productRepository;
    private readonly MeetingTypeRepository _meetingTypeRepository;
    private readonly FollowUpTaskRepository _followUpTaskRepository;
    private readonly ILeadRequestService _leadRequestService;
    private readonly ICurrentTenantService _tenantService;

    public MeetingService(
        MeetingRepository meetingRepository,
        MeetingProductRequestRepository productRequestRepository,
        MeetingOpportunityRepository opportunityRepository,
        SalesContactRepository contactRepository,
        SalesProductRepository productRepository,
        MeetingTypeRepository meetingTypeRepository,
        FollowUpTaskRepository followUpTaskRepository,
        ILeadRequestService leadRequestService,
        ICurrentTenantService tenantService)
    {
        _meetingRepository = meetingRepository;
        _productRequestRepository = productRequestRepository;
        _opportunityRepository = opportunityRepository;
        _contactRepository = contactRepository;
        _productRepository = productRepository;
        _meetingTypeRepository = meetingTypeRepository;
        _followUpTaskRepository = followUpTaskRepository;
        _leadRequestService = leadRequestService;
        _tenantService = tenantService;
    }

    public async Task<ServiceResult> CreateMeetingAsync(CreateMeetingRequest request, string userId)
    {
        try
        {
            var entity = new Meeting
            {
                BusinessId = _tenantService.CurrentBusinessId,
                LeadRequestId = request.LeadRequestId,
                ContactId = request.ContactId,
                MeetingTypeId = request.MeetingTypeId,
                Subject = request.Subject,
                ScheduledAtUtc = request.ScheduledAtUtc,
                DurationMinutes = request.DurationMinutes,
                Location = request.Location,
                Notes = request.Notes,
                IsCancelled = false,
                IsActive = true,
                CreatedByUserId = userId
            };

            var id = await _meetingRepository.InsertAsync(entity);

            // Suggest stage transition when linked to a lead
            if (request.LeadRequestId.HasValue)
            {
                await _leadRequestService.SuggestStageTransitionAsync(request.LeadRequestId.Value, "meeting_scheduled", id);
            }

            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateMeetingAsync(UpdateMeetingRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var existing = await _meetingRepository.GetByIdAsync(request.Id, businessId);
            if (existing == null)
                return ServiceResult.Fail("Meeting not found.");

            existing.MeetingTypeId = request.MeetingTypeId;
            existing.Subject = request.Subject;
            existing.ScheduledAtUtc = request.ScheduledAtUtc;
            existing.DurationMinutes = request.DurationMinutes;
            existing.Location = request.Location;
            existing.Notes = request.Notes;
            existing.Outcome = request.Outcome;
            existing.MeetingOutcomeClassificationId = request.MeetingOutcomeClassificationId;

            await _meetingRepository.UpdateAsync(existing);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CancelMeetingAsync(int id, string? description)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var meeting = await _meetingRepository.GetByIdAsync(id, businessId);
            if (meeting == null)
                return ServiceResult.Fail("Meeting not found.");

            await _meetingRepository.CancelAsync(id, businessId, description);

            if (meeting.LeadRequestId.HasValue)
            {
                await _leadRequestService.ReevaluateStageOnMeetingChangeAsync(meeting.LeadRequestId.Value, "meeting_cancelled", id);
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ReactivateMeetingAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var meeting = await _meetingRepository.GetByIdAsync(id, businessId);
            if (meeting == null)
                return ServiceResult.Fail("Meeting not found.");

            await _meetingRepository.ReactivateAsync(id, businessId);

            if (meeting.LeadRequestId.HasValue)
            {
                await _leadRequestService.ReevaluateStageOnMeetingChangeAsync(meeting.LeadRequestId.Value, "meeting_reactivated", id);
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<MeetingDetailDto?> GetByIdAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var meeting = await _meetingRepository.GetByIdAsync(id, businessId);
            if (meeting == null) return null;

            var contact = await _contactRepository.GetByIdAsync(meeting.ContactId, businessId);
            var meetingTypes = await _meetingTypeRepository.GetAllAsync();
            var meetingType = meetingTypes.FirstOrDefault(mt => mt.Id == meeting.MeetingTypeId);

            var productRequests = await _productRequestRepository.GetByMeetingIdAsync(id);
            var opportunities = await _opportunityRepository.GetByMeetingIdAsync(id);
            var linkedTasks = await _followUpTaskRepository.GetByMeetingIdAsync(id, businessId);

            var productRequestDtos = new List<MeetingProductRequestDto>();
            foreach (var pr in productRequests)
            {
                var product = await _productRepository.GetByIdAsync(pr.ProductId, businessId);
                productRequestDtos.Add(new MeetingProductRequestDto
                {
                    Id = pr.Id,
                    ProductName = product?.Name ?? "Unknown",
                    RequestText = pr.RequestText,
                    CreatedAtUtc = pr.CreatedAtUtc
                });
            }

            return new MeetingDetailDto
            {
                Id = meeting.Id,
                LeadRequestId = meeting.LeadRequestId,
                ContactId = meeting.ContactId,
                ContactName = contact != null
                    ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                    : "Unknown",
                MeetingTypeId = meeting.MeetingTypeId,
                MeetingTypeName = meetingType?.Name ?? "Unknown",
                Subject = meeting.Subject,
                ScheduledAtUtc = meeting.ScheduledAtUtc,
                DurationMinutes = meeting.DurationMinutes,
                Location = meeting.Location,
                Notes = meeting.Notes,
                Outcome = meeting.Outcome,
                MeetingOutcomeClassificationId = meeting.MeetingOutcomeClassificationId,
                IsCancelled = meeting.IsCancelled,
                CreatedAtUtc = meeting.CreatedAtUtc,
                ProductRequests = productRequestDtos,
                Opportunities = opportunities.Select(o => new MeetingOpportunityDto
                {
                    Id = o.Id,
                    Title = o.Title,
                    Description = o.Description,
                    EstimatedValue = o.EstimatedValue,
                    CreatedAtUtc = o.CreatedAtUtc
                }).ToList(),
                Tasks = linkedTasks.Select(t => new MeetingTaskBriefDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    TaskType = t.TaskType,
                    DueAtUtc = t.DueAtUtc,
                    ScheduledTimeUtc = t.ScheduledTimeUtc,
                    IsCompleted = t.IsCompleted,
                    CompletedAtUtc = t.CompletedAtUtc,
                    TaskOutcome = t.TaskOutcome
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<string?> GetSubjectAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var subjects = await _meetingRepository.GetSubjectsByIdsAsync(new[] { id }, businessId);
            return subjects.TryGetValue(id, out var subject) ? subject : null;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<MeetingListDto>> GetMeetingsForLeadAsync(int leadRequestId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var meetings = await _meetingRepository.GetByLeadRequestIdAsync(leadRequestId, businessId);
            return await MapToListDtos(meetings, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<MeetingListDto>> GetAllMeetingsAsync()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var meetings = await _meetingRepository.GetAllByBusinessIdAsync(businessId);
            return await MapToListDtos(meetings, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<byte[]> GenerateIcsFileAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var meeting = await _meetingRepository.GetByIdAsync(id, businessId);
            if (meeting == null)
                return Array.Empty<byte>();

            var contact = await _contactRepository.GetByIdAsync(meeting.ContactId, businessId);
            var contactName = contact != null
                ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                : "Unknown";

            var endTime = meeting.ScheduledAtUtc.AddMinutes(meeting.DurationMinutes);

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//3Inventors//Portal//EN");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:REQUEST");
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"DTSTART:{meeting.ScheduledAtUtc:yyyyMMdd'T'HHmmss'Z'}");
            sb.AppendLine($"DTEND:{endTime:yyyyMMdd'T'HHmmss'Z'}");
            sb.AppendLine($"SUMMARY:{EscapeIcs(meeting.Subject)}");
            sb.AppendLine($"DESCRIPTION:{EscapeIcs(meeting.Notes ?? string.Empty)}");
            sb.AppendLine($"LOCATION:{EscapeIcs(meeting.Location ?? string.Empty)}");
            sb.AppendLine($"UID:{meeting.Id}@portal.3inventors.com");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}");
            sb.AppendLine($"ORGANIZER:CN={contactName}");
            sb.AppendLine("STATUS:CONFIRMED");
            sb.AppendLine("END:VEVENT");
            sb.AppendLine("END:VCALENDAR");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateProductRequestAsync(CreateMeetingProductRequestDto request)
    {
        try
        {
            var entity = new MeetingProductRequest
            {
                MeetingId = request.MeetingId,
                ProductId = request.ProductId,
                RequestText = request.RequestText,
                IsActive = true,
                IsCancelled = false
            };

            var id = await _productRequestRepository.InsertAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateOpportunityAsync(CreateMeetingOpportunityDto request)
    {
        try
        {
            var entity = new MeetingOpportunity
            {
                MeetingId = request.MeetingId,
                Title = request.Title,
                Description = request.Description,
                EstimatedValue = request.EstimatedValue,
                IsActive = true
            };

            var id = await _opportunityRepository.InsertAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<MeetingBriefDto>> GetUpcomingMeetingsBriefAsync(int businessId)
    {
        try
        {
            var todayStart = DateTime.UtcNow.Date;
            var endDate = todayStart.AddDays(4);

            var meetings = await _meetingRepository.GetUpcomingBriefAsync(businessId, todayStart, endDate);

            var meetingTypes = await _meetingTypeRepository.GetAllAsync();
            var contactIds = meetings.Select(m => m.ContactId).Distinct();
            var contactsLookup = await _contactRepository.GetByIdsAsync(contactIds, businessId);

            var briefs = new List<MeetingBriefDto>();

            foreach (var m in meetings)
            {
                contactsLookup.TryGetValue(m.ContactId, out var contact);
                var meetingType = meetingTypes.FirstOrDefault(mt => mt.Id == m.MeetingTypeId);

                briefs.Add(new MeetingBriefDto
                {
                    Id = m.Id,
                    LeadRequestId = m.LeadRequestId,
                    ContactId = m.ContactId,
                    Subject = m.Subject,
                    ContactName = contact != null
                        ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                        : "Unknown",
                    MeetingTypeName = meetingType?.Name ?? "Unknown",
                    ScheduledAtUtc = m.ScheduledAtUtc,
                    DurationMinutes = m.DurationMinutes,
                    Location = m.Location
                });
            }

            return briefs;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<DashboardMeetingBriefDto>> GetDashboardMeetingsBriefAsync(int businessId)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var dayAfterTomorrow = today.AddDays(2);

            var meetings = await _meetingRepository.GetDashboardMeetingsBriefAsync(businessId, today, dayAfterTomorrow);

            var meetingTypes = await _meetingTypeRepository.GetAllAsync();
            var contactIds = meetings.Select(m => m.ContactId).Distinct();
            var contactsLookup = await _contactRepository.GetByIdsAsync(contactIds, businessId);

            var briefs = new List<DashboardMeetingBriefDto>();

            foreach (var m in meetings)
            {
                contactsLookup.TryGetValue(m.ContactId, out var contact);
                var meetingType = meetingTypes.FirstOrDefault(mt => mt.Id == m.MeetingTypeId);

                briefs.Add(new DashboardMeetingBriefDto
                {
                    Id = m.Id,
                    Subject = m.Subject,
                    ContactName = contact != null
                        ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                        : "Unknown",
                    MeetingTypeName = meetingType?.Name ?? "Unknown",
                    ScheduledAtUtc = m.ScheduledAtUtc,
                    DurationMinutes = m.DurationMinutes,
                    Urgency = m.ScheduledAtUtc.Date == today ? "today" : "tomorrow"
                });
            }

            return briefs;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<MeetingPagedListDto>> GetMeetingsPagedAsync(MeetingFilter filter, int page, int pageSize)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var (items, totalCount) = await _meetingRepository.GetPagedAsync(
                businessId, filter.Status, filter.MeetingTypeId,
                filter.DateFrom, filter.DateTo, filter.OutcomeClassificationId, page, pageSize);

            var meetingTypes = await _meetingTypeRepository.GetAllAsync();
            var contactIds = items.Select(m => m.ContactId).Distinct();
            var contactsLookup = await _contactRepository.GetByIdsAsync(contactIds, businessId);

            // Batch-fetch task counts for all meetings on this page
            var meetingIds = items.Select(m => m.Id);
            var taskCounts = await _followUpTaskRepository.GetTaskCountsByMeetingIdsAsync(meetingIds, businessId);

            var now = DateTime.UtcNow;
            var today = now.Date;

            var dtos = items.Select(m =>
            {
                contactsLookup.TryGetValue(m.ContactId, out var contact);
                var meetingType = meetingTypes.FirstOrDefault(mt => mt.Id == m.MeetingTypeId);

                string urgency;
                if (m.IsCancelled)
                    urgency = "cancelled";
                else if (m.ScheduledAtUtc.Date == today && !m.IsCancelled)
                    urgency = "today";
                else if (m.ScheduledAtUtc > now && !m.IsCancelled)
                    urgency = "upcoming";
                else if (m.ScheduledAtUtc < now && !m.IsCancelled && m.Outcome == null)
                    urgency = "needs_outcome";
                else
                    urgency = "completed";

                var tc = taskCounts.TryGetValue(m.Id, out var taskCount) ? taskCount : (Total: 0, Pending: 0);

                return new MeetingPagedListDto
                {
                    Id = m.Id,
                    Subject = m.Subject,
                    MeetingTypeName = meetingType?.Name ?? "Unknown",
                    MeetingTypeId = m.MeetingTypeId,
                    ContactName = contact != null
                        ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                        : "Unknown",
                    ContactId = m.ContactId,
                    LeadRequestId = m.LeadRequestId,
                    ScheduledAtUtc = m.ScheduledAtUtc,
                    DurationMinutes = m.DurationMinutes,
                    Location = m.Location,
                    Notes = m.Notes,
                    Outcome = m.Outcome,
                    MeetingOutcomeClassificationId = m.MeetingOutcomeClassificationId,
                    OutcomeClassificationName = ResolveClassificationName(m.MeetingOutcomeClassificationId),
                    IsCancelled = m.IsCancelled,
                    TaskCount = tc.Total,
                    PendingTaskCount = tc.Pending,
                    Urgency = urgency
                };
            }).ToList();

            return new PagedResult<MeetingPagedListDto>
            {
                Items = dtos,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task<List<MeetingListDto>> MapToListDtos(List<Meeting> meetings, int businessId)
    {
        var meetingTypes = await _meetingTypeRepository.GetAllAsync();
        var dtos = new List<MeetingListDto>();

        foreach (var m in meetings)
        {
            var contact = await _contactRepository.GetByIdAsync(m.ContactId, businessId);
            var meetingType = meetingTypes.FirstOrDefault(mt => mt.Id == m.MeetingTypeId);

            dtos.Add(new MeetingListDto
            {
                Id = m.Id,
                Subject = m.Subject,
                MeetingTypeName = meetingType?.Name ?? "Unknown",
                ContactName = contact != null
                    ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                    : "Unknown",
                ScheduledAtUtc = m.ScheduledAtUtc,
                DurationMinutes = m.DurationMinutes,
                Outcome = m.Outcome,
                IsCancelled = m.IsCancelled,
                CreatedAtUtc = m.CreatedAtUtc
            });
        }

        return dtos;
    }

    private static string EscapeIcs(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\n", "\\n")
            .Replace("\r", string.Empty);
    }

    private static readonly Dictionary<int, string> _classificationNames = new()
    {
        { 1, "Positive" },
        { 2, "Neutral" },
        { 3, "Negative" },
        { 4, "Rescheduled" },
        { 5, "No Show" }
    };

    private static string? ResolveClassificationName(int? classificationId)
    {
        if (!classificationId.HasValue) return null;
        return _classificationNames.TryGetValue(classificationId.Value, out var name) ? name : null;
    }
}
