namespace Portal.Infrastructure.Entities;

/// <summary>
/// Reference table defining the lifecycle states of a Quotation.
/// Schema: [quotation].QuotationStatusType
/// Seed values: Draft (1), Sent (2), Accepted (3), Converted (4), Archived (5)
/// </summary>
public class QuotationStatusType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    // Navigation properties
    public ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
}
