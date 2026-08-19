namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// DTO for displaying a meeting in a list.
/// </summary>
public class MeetingListDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = null!;
    public string MeetingTypeName { get; set; } = null!;
    public string ContactName { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }
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
}

/// <summary>
/// Request model for updating a meeting.
/// </summary>
public class UpdateMeetingRequest
{
    public int Id { get; set; }
    public int MeetingTypeId { get; set; }
    public string Subject { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
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
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<MeetingProductRequestDto> ProductRequests { get; set; } = new();
    public List<MeetingOpportunityDto> Opportunities { get; set; } = new();
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
