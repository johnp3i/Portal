namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A scheduled meeting between the business and a contact, optionally linked to a lead.
/// Schema: [sales].[Meeting]
/// </summary>
public class Meeting
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int? LeadRequestId { get; set; }

    public int ContactId { get; set; }

    public int MeetingTypeId { get; set; }

    public string Subject { get; set; } = null!;

    public DateTime ScheduledAtUtc { get; set; }

    public int DurationMinutes { get; set; }

    public string? Location { get; set; }

    public string? Notes { get; set; }

    public string? Outcome { get; set; }

    public bool IsCancelled { get; set; }

    public DateTime? CancellationTimestamp { get; set; }

    public string? CancellationDescription { get; set; }

    public bool IsActive { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public LeadRequest? LeadRequest { get; set; }

    public SalesContact Contact { get; set; } = null!;

    public MeetingType MeetingType { get; set; } = null!;

    public ICollection<MeetingProductRequest> ProductRequests { get; set; } = new List<MeetingProductRequest>();

    public ICollection<MeetingOpportunity> Opportunities { get; set; } = new List<MeetingOpportunity>();
}
