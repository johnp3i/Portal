namespace Portal.Infrastructure.Models;

/// <summary>
/// A single activity entry for the business manager Activity Log timeline.
/// </summary>
public class ActivityItemDto
{
    public long Id { get; set; }
    public string Summary { get; set; } = null!;
    public string ActorName { get; set; } = null!;
    public string ActionType { get; set; } = null!; // "Created", "Edited", "Deleted", "StatusChanged"
    public string EntityType { get; set; } = null!; // Business-friendly: "Invoice", "Customer", etc.
    public string EntityId { get; set; } = null!;
    public string? EntityDisplayRef { get; set; } // Human-readable: "INV-2026-0089"
    public string? EntityDetailUrl { get; set; } // Link to detail page, null if deleted/unknown
    public DateTime TimestampUtc { get; set; }
    public string? OldValues { get; set; } // Raw JSON
    public string? NewValues { get; set; } // Raw JSON
    public List<FieldChangeDto>? ChangedFields { get; set; } // Parsed for detail panel
    public bool IsStatusChange { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
}

/// <summary>
/// A single field change within an activity entry (for expanded detail panels).
/// </summary>
public class FieldChangeDto
{
    public string FieldName { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
