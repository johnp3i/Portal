namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// A single timeline event for the unified timeline view.
/// </summary>
public class TimelineEventDto
{
    public string EventType { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string ActorName { get; set; } = "System";
    public string Colour { get; set; } = null!;
}
