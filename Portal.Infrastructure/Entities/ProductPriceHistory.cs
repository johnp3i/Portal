namespace Portal.Infrastructure.Entities;

/// <summary>
/// A historical record capturing each change to a product's selling or cost price.
/// Schema: [product].ProductPriceHistory
/// </summary>
public class ProductPriceHistory
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public decimal SellingPrice { get; set; }

    public decimal CostPrice { get; set; }

    public DateTime EffectiveFromUtc { get; set; }

    public string ChangedByUserId { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public int? ProductPriceTierId { get; set; }

    // Navigation properties
    public Product Product { get; set; } = null!;

    public ProductPriceTier? ProductPriceTier { get; set; }
}
