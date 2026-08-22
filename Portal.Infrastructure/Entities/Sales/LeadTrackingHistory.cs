namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Entity for [sales].[LeadTrackingHistory] table. Records every lead stage transition.
/// </summary>
public class LeadTrackingHistory
{
    public int Id { get; set; }

    public int LeadRequestId { get; set; }

    public int BusinessId { get; set; }

    public int LeadTrackingActionTypeId { get; set; }

    public int? FromLeadStatusTypeId { get; set; }

    public int ToLeadStatusTypeId { get; set; }

    public int? RelatedEntityId { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
