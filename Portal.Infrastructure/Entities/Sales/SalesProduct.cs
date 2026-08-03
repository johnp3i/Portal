namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A product in the business's sales catalogue used for pipeline tracking.
/// Schema: [sales].[Product]
/// </summary>
public class SalesProduct
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Optional link to the Product Catalog for reference pricing. NULL if no catalog link.
    /// </summary>
    public int? ProductId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ICollection<LeadRequest> LeadRequests { get; set; } = new List<LeadRequest>();

    public ICollection<LeadResponseTemplate> Templates { get; set; } = new List<LeadResponseTemplate>();

    public ICollection<MeetingProductRequest> MeetingProductRequests { get; set; } = new List<MeetingProductRequest>();
}
