namespace Portal.Infrastructure.Entities;

/// <summary>
/// A named pricing level for a product (e.g., Retail, Wholesale, VIP).
/// Schema: [product].[ProductPriceTier]
/// </summary>
public class ProductPriceTier
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string TierName { get; set; } = null!;
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    public Product Product { get; set; } = null!;
}
