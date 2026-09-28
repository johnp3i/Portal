namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// DTO for displaying a meeting in a list.
/// </summary>
public class MeetingListDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = null!;
    public string MeetingTypeName { get; set; } = null!;
    public int ContactId { get; set; }
    public string ContactName { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }
    /// <summary>Precomputed (business-local) upcoming flag so views need not compare to UtcNow.</summary>
    public bool IsUpcoming { get; set; }
    public int DurationMinutes { get; set; }
    public string? Outcome { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Request model for creating a meeting.
/// </summary>
public class CreateMeetingRequest
{
    public int? LeadRequestId { get; set; }
    public int ContactId { get; set; }
    public int MeetingTypeId { get; set; }
    public string Subject { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public string? Location { get; set; }
    public string? Notes { get; set; }

    /// <summary>Team members assigned to attend (many-to-many). Empty = unassigned.</summary>
    public List<int> AttendeeTeamMemberIds { get; set; } = new();
}

/// <summary>
/// Request model for updating a meeting.
/// </summary>
public class UpdateMeetingRequest
{
    public int Id { get; set; }
    public int? LeadRequestId { get; set; }
    public int MeetingTypeId { get; set; }
    public string Subject { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
    public int? MeetingOutcomeClassificationId { get; set; }

    /// <summary>Team members assigned to attend (replace-on-save). Empty = unassigned.</summary>
    public List<int> AttendeeTeamMemberIds { get; set; } = new();
}

/// <summary>
/// Detailed meeting view including product requests and opportunities.
/// </summary>
public class MeetingDetailDto
{
    public int Id { get; set; }
    public int? LeadRequestId { get; set; }
    public int ContactId { get; set; }
    public string ContactName { get; set; } = null!;
    public int MeetingTypeId { get; set; }
    public string MeetingTypeName { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }

    /// <summary>
    /// Scheduled time in the business's local zone as yyyy-MM-ddTHH:mm, for prefilling the edit
    /// form's &lt;input type="datetime-local"&gt; without depending on the browser's time zone.
    /// </summary>
    public string ScheduledLocalInput { get; set; } = null!;

    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
    public int? MeetingOutcomeClassificationId { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<MeetingProductRequestDto> ProductRequests { get; set; } = new();
    public List<MeetingOpportunityDto> Opportunities { get; set; } = new();
    public List<MeetingTaskBriefDto> Tasks { get; set; } = new();

    /// <summary>Ids of team members assigned to attend this meeting.</summary>
    public List<int> AttendeeTeamMemberIds { get; set; } = new();
}

/// <summary>
/// A product interest captured during a meeting.
/// </summary>
public class MeetingProductRequestDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = null!;
    public string? RequestText { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// A business opportunity captured during a meeting.
/// </summary>
public class MeetingOpportunityDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal? EstimatedValue { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Request model for creating a meeting product request.
/// </summary>
public class CreateMeetingProductRequestDto
{
    public int MeetingId { get; set; }
    public int ProductId { get; set; }
    public string? RequestText { get; set; }
}

/// <summary>
/// Request model for creating a meeting opportunity.
/// </summary>
public class CreateMeetingOpportunityDto
{
    public int MeetingId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal? EstimatedValue { get; set; }
}

/// <summary>
/// Lightweight task representation for the meeting detail response.
/// </summary>
public class MeetingTaskBriefDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public byte FollowUpTaskTypeId { get; set; }
    public string TaskType { get; set; } = null!;
    public DateTime DueAtUtc { get; set; }
    public TimeOnly? ScheduledTimeUtc { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? TaskOutcome { get; set; }
}

/// <summary>
/// Brief DTO for the Upcoming Meetings panel on the Pipeline page.
/// </summary>
public class MeetingBriefDto
{
    public int Id { get; set; }
    public int? LeadRequestId { get; set; }
    public int ContactId { get; set; }
    public string Subject { get; set; } = null!;
    public string ContactName { get; set; } = null!;
    public string MeetingTypeName { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Brief DTO for dashboard Today's Brief section — meetings scheduled today/tomorrow.
/// </summary>
public class DashboardMeetingBriefDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = null!;
    public string ContactName { get; set; } = null!;
    public string MeetingTypeName { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }
    public int DurationMinutes { get; set; }

    /// <summary>"today" or "tomorrow"</summary>
    public string Urgency { get; set; } = null!;
}

/// <summary>
/// Filter model for the meetings paged list.
/// </summary>
public class MeetingFilter
{
    public string? Status { get; set; }
    public int? MeetingTypeId { get; set; }
    public int? OutcomeClassificationId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

/// <summary>
/// DTO for the paginated meetings table with urgency and contact details.
/// </summary>
public class MeetingPagedListDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = null!;
    public string MeetingTypeName { get; set; } = null!;
    public int MeetingTypeId { get; set; }
    public string ContactName { get; set; } = null!;
    public int ContactId { get; set; }
    public int? LeadRequestId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }

    /// <summary>
    /// The scheduled time in the business's local zone, preformatted for display
    /// (e.g. "30 Sep 2026, 10:30"). The client renders this verbatim so display does not depend
    /// on the browser's time zone.
    /// </summary>
    public string ScheduledDisplay { get; set; } = null!;

    /// <summary>
    /// The scheduled time in the business's local zone as an ISO datetime-local string
    /// (yyyy-MM-ddTHH:mm) for prefilling the edit form's &lt;input type="datetime-local"&gt;.
    /// </summary>
    public string ScheduledLocalInput { get; set; } = null!;

    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
    public int? MeetingOutcomeClassificationId { get; set; }
    public string? OutcomeClassificationName { get; set; }
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Total count of tasks linked to this meeting.
    /// </summary>
    public int TaskCount { get; set; }

    /// <summary>
    /// Count of pending (incomplete) tasks linked to this meeting.
    /// </summary>
    public int PendingTaskCount { get; set; }

    /// <summary>
    /// Computed urgency: "today", "upcoming", "needs_outcome", "completed", "cancelled"
    /// </summary>
    public string Urgency { get; set; } = null!;
}
