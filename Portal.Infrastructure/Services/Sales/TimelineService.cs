using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Aggregates timeline events from LeadResponse, Meeting, ActivityFeed, and LeadRequest
/// into a unified, paginated, descending-chronological view.
/// No duplication: responses/meetings ONLY from entity tables, all others ONLY from ActivityFeed.
/// </summary>
public class TimelineService : ITimelineService
{
    private readonly ICurrentTenantService _tenantService;
    private readonly LeadResponseRepository _leadResponseRepository;
    private readonly MeetingRepository _meetingRepository;
    private readonly ActivityFeedRepository _activityFeedRepository;
    private readonly TeamMemberRepository _teamMemberRepository;
    private readonly LeadRequestRepository _leadRequestRepository;

    // ActivityFeed action → (EventType, Colour) mapping
    private static readonly Dictionary<string, (string EventType, string Colour)> ActivityFeedActionMap = new()
    {
        { "stage_changed", ("stage_change", "#0D5EA6") },
        { "assigned", ("assignment", "#0D5EA6") },
        { "unassigned", ("assignment", "#0D5EA6") },
        { "proposal_linked", ("proposal_linked", "#57B8E8") },
        { "invoice_linked", ("invoice_linked", "#57B8E8") },
        { "marked_as_won", ("conversion", "#129867") },
        { "task_created", ("task", "#8a9bab") },
        { "lead_cancelled", ("cancellation", "#C24A4A") },
        { "lead_reactivated", ("reactivation", "#129867") },
        { "request_details_updated", ("update", "#8a9bab") }
    };

    public TimelineService(
        ICurrentTenantService tenantService,
        LeadResponseRepository leadResponseRepository,
        MeetingRepository meetingRepository,
        ActivityFeedRepository activityFeedRepository,
        TeamMemberRepository teamMemberRepository,
        LeadRequestRepository leadRequestRepository)
    {
        _tenantService = tenantService;
        _leadResponseRepository = leadResponseRepository;
        _meetingRepository = meetingRepository;
        _activityFeedRepository = activityFeedRepository;
        _teamMemberRepository = teamMemberRepository;
        _leadRequestRepository = leadRequestRepository;
    }

    public async Task<PagedResult<TimelineEventDto>> GetTimelineAsync(int leadRequestId, int page, int pageSize)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            // Fetch all sources sequentially (DbContext is not thread-safe)
            var leadRequest = await _leadRequestRepository.GetByIdAsync(leadRequestId, businessId);
            var responses = await _leadResponseRepository.GetByLeadRequestIdAsync(leadRequestId);
            var meetings = await _meetingRepository.GetByLeadRequestIdAsync(leadRequestId, businessId);
            var activityFeedEntries = await _activityFeedRepository.GetByLeadRequestIdAsync(leadRequestId);
            var teamMembers = await _teamMemberRepository.GetAllByBusinessIdAsync(businessId);

            // Build team member lookup for actor resolution
            var teamMemberLookup = teamMembers.ToDictionary(tm => tm.Id, tm => BuildFullName(tm));

            // Build UserId → Name lookup for response actor resolution
            var userIdToNameLookup = teamMembers
                .Where(tm => !string.IsNullOrEmpty(tm.UserId))
                .GroupBy(tm => tm.UserId!)
                .ToDictionary(g => g.Key, g => BuildFullName(g.First()));

            // Aggregate all events
            var allEvents = new List<TimelineEventDto>();

            // 1. Response events (from entity table ONLY)
            allEvents.AddRange(MapResponseEvents(responses, teamMemberLookup, userIdToNameLookup));

            // 2. Meeting events (from entity table ONLY)
            allEvents.AddRange(MapMeetingEvents(meetings));

            // 3. ActivityFeed events (everything else ONLY from here)
            allEvents.AddRange(MapActivityFeedEvents(activityFeedEntries, teamMemberLookup));

            // 4. Synthetic "creation" event from LeadRequest.CreatedAtUtc
            if (leadRequest != null)
            {
                allEvents.Add(new TimelineEventDto
                {
                    EventType = "creation",
                    Timestamp = leadRequest.CreatedAtUtc,
                    Title = "Lead created",
                    Description = null,
                    ActorName = "System",
                    Colour = "#8a9bab"
                });
            }

            // Sort all events by Timestamp descending
            var sortedEvents = allEvents.OrderByDescending(e => e.Timestamp).ToList();

            // Apply pagination
            var totalCount = sortedEvents.Count;
            var pagedItems = sortedEvents
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<TimelineEventDto>
            {
                Items = pagedItems,
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

    private List<TimelineEventDto> MapResponseEvents(List<LeadResponse> responses, Dictionary<int, string> teamMemberLookup, Dictionary<string, string> userIdToNameLookup)
    {
        return responses.Select(r => new TimelineEventDto
        {
            EventType = "response",
            Timestamp = r.SentAtUtc,
            Title = "Response sent",
            Description = TruncateText(r.ResponseText, 200),
            ActorName = ResolveResponseActor(r.RespondedByUserId, userIdToNameLookup),
            Colour = "#129867"
        }).ToList();
    }

    private List<TimelineEventDto> MapMeetingEvents(List<Meeting> meetings)
    {
        return meetings.Select(m => new TimelineEventDto
        {
            EventType = "meeting",
            Timestamp = m.ScheduledAtUtc,
            Title = m.Subject,
            Description = m.Outcome,
            ActorName = "System",
            Colour = "#C8912E"
        }).ToList();
    }

    private List<TimelineEventDto> MapActivityFeedEvents(List<ActivityFeedEntry> entries, Dictionary<int, string> teamMemberLookup)
    {
        var events = new List<TimelineEventDto>();

        foreach (var entry in entries)
        {
            if (ActivityFeedActionMap.TryGetValue(entry.Action, out var mapping))
            {
                events.Add(new TimelineEventDto
                {
                    EventType = mapping.EventType,
                    Timestamp = entry.CreatedAtUtc,
                    Title = FormatActivityTitle(entry.Action),
                    Description = entry.Description,
                    ActorName = ResolveActivityFeedActor(entry.PerformedByTeamMemberId, teamMemberLookup),
                    Colour = mapping.Colour
                });
            }
        }

        return events;
    }

    private static string ResolveResponseActor(string? respondedByUserId, Dictionary<string, string> userIdToNameLookup)
    {
        if (string.IsNullOrEmpty(respondedByUserId))
            return "System";

        return userIdToNameLookup.TryGetValue(respondedByUserId, out var name)
            ? name
            : "System";
    }

    private static string ResolveActivityFeedActor(int? performedByTeamMemberId, Dictionary<int, string> teamMemberLookup)
    {
        if (!performedByTeamMemberId.HasValue)
            return "System";

        return teamMemberLookup.TryGetValue(performedByTeamMemberId.Value, out var name)
            ? name
            : "System";
    }

    private static string BuildFullName(TeamMember teamMember)
    {
        if (string.IsNullOrWhiteSpace(teamMember.LastName))
            return teamMember.FirstName;

        return $"{teamMember.FirstName} {teamMember.LastName}";
    }

    private static string FormatActivityTitle(string action)
    {
        return action switch
        {
            "stage_changed" => "Stage changed",
            "assigned" => "Lead assigned",
            "unassigned" => "Lead unassigned",
            "proposal_linked" => "Proposal linked",
            "invoice_linked" => "Invoice linked",
            "marked_as_won" => "Marked as won",
            "task_created" => "Task created",
            "lead_cancelled" => "Lead cancelled",
            "lead_reactivated" => "Lead reactivated",
            "request_details_updated" => "Details updated",
            _ => action
        };
    }

    private static string? TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "…";
    }
}
