namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Links a meeting to a product the contact expressed interest in.
/// Schema: [sales].[MeetingProductRequest]
/// </summary>
public class MeetingProductRequest
{
    public int Id { get; set; }

    public int MeetingId { get; set; }

    public int ProductId { get; set; }

    public string? RequestText { get; set; }

    public bool IsActive { get; set; }

    public bool IsCancelled { get; set; }

    public DateTime? CancellationTimestamp { get; set; }

    public string? CancellationDescription { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Meeting Meeting { get; set; } = null!;

    public SalesProduct Product { get; set; } = null!;
}
