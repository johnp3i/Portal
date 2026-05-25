namespace Portal.Infrastructure.Entities;

/// <summary>
/// A master catalog record representing a sellable item or service, scoped to a business tenant.
/// Schema: [product].Product
/// </summary>
public class Product
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string ProductCode { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal DefaultSellingPrice { get; set; }

    public decimal DefaultCostPrice { get; set; }

    public decimal DefaultVatRate { get; set; }

    public int? SupplierId { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastUsedDate { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Supplier? Supplier { get; set; }

    public ICollection<ProductPriceHistory> PriceHistory { get; set; } = new List<ProductPriceHistory>();
}
