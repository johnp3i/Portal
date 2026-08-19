namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// An inbound lead enquiry from a contact. Tracks source, status, product interest, and assignment.
/// Schema: [sales].[LeadRequest]
/// </summary>
public class LeadRequest
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int ContactId { get; set; }

    public int? ProductId { get; set; }

    public int LeadSourceTypeId { get; set; }

    public int? LeadSourceReferenceTypeId { get; set; }

    public int LeadStatusTypeId { get; set; }

    public string? SourceUrl { get; set; }

    public string? RequestText { get; set; }

    public string? AssignedToUserId { get; set; }

    public int? TeamMemberId { get; set; }

    public bool IsCancelled { get; set; }

    public DateTime? CancellationTimestamp { get; set; }

    public string? CancellationDescription { get; set; }

    public bool IsActive { get; set; }

    public int? LeadPriorityTypeId { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public SalesContact Contact { get; set; } = null!;

    public TeamMember? TeamMember { get; set; }

    public SalesProduct? Product { get; set; }

    public LeadSourceType LeadSourceType { get; set; } = null!;

    public LeadSourceReferenceType? LeadSourceReferenceType { get; set; }

    public LeadStatusType LeadStatusType { get; set; } = null!;

    public LeadPriorityType? LeadPriorityType { get; set; }

    public ICollection<LeadResponse> Responses { get; set; } = new List<LeadResponse>();

    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
}
