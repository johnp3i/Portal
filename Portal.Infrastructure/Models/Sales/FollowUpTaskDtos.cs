namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// DTO for displaying a follow-up task in lists and panels.
/// </summary>
public class FollowUpTaskDto
{
    public int Id { get; set; }
    public int? LeadRequestId { get; set; }
    public string? ContactName { get; set; }
    public string? LeadProductName { get; set; }
    public string? AssignedToName { get; set; }
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = null!;
    public DateTime DueAtUtc { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int SnoozedCount { get; set; }

    /// <summary>
    /// Computed urgency: "overdue", "today", "tomorrow", "upcoming"
    /// </summary>
    public string Urgency { get; set; } = null!;
}

/// <summary>
/// Request model for creating a follow-up task.
/// </summary>
public class CreateFollowUpTaskRequest
{
    public int? LeadRequestId { get; set; }
    public int? ContactId { get; set; }
    public int? TeamMemberId { get; set; }
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = null!;
    public DateTime DueAtUtc { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request model for snoozing a task.
/// </summary>
public class SnoozeFollowUpTaskRequest
{
    public int Id { get; set; }
    public DateTime NewDueDate { get; set; }
}

/// <summary>
/// Filter model for the tasks paged list.
/// </summary>
public class FollowUpTaskFilter
{
    public string? Status { get; set; } // pending, completed, overdue
    public string? TaskType { get; set; }
    public int? TeamMemberId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

/// <summary>
/// Request model for updating a follow-up task.
/// </summary>
public class UpdateFollowUpTaskRequest
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = null!;
    public DateTime DueAtUtc { get; set; }
    public string? Notes { get; set; }
}
