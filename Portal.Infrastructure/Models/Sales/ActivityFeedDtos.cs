namespace Portal.Infrastructure.Models.Sales;

public class ActivityFeedDto
{
    public int Id { get; set; }
    public string Action { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? PerformedByName { get; set; }
    public string? LeadName { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Input model for recording an activity feed entry.
/// </summary>
public class ActivityEntry
{
    public int BusinessId { get; set; }
    public int LeadRequestId { get; set; }
    public string Action { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? PerformedByUserId { get; set; }
    public int? PerformedByTeamMemberId { get; set; }
    public string? Metadata { get; set; }
}

/// <summary>
/// Filter model for the activity feed paged list.
/// </summary>
public class ActivityFeedFilter
{
    public string? ActionType { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

/// <summary>
/// DTO for the paginated activity feed table with lead name resolution.
/// </summary>
public class ActivityFeedPageDto
{
    public int Id { get; set; }
    public string Action { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? PerformedByName { get; set; }
    public string? LeadName { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
