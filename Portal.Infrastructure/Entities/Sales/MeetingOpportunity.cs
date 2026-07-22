namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A business opportunity identified during a meeting. Tracks title, description, and estimated value.
/// Schema: [sales].[MeetingOpportunity]
/// </summary>
public class MeetingOpportunity
{
    public int Id { get; set; }

    public int MeetingId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public decimal? EstimatedValue { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Meeting Meeting { get; set; } = null!;
}
