namespace Portal.Infrastructure.Models;

public class CreateTierRequest
{
    public int ProductId { get; set; }
    public string TierName { get; set; } = null!;
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateTierRequest
{
    public int TierId { get; set; }
    public int ProductId { get; set; }
    public string TierName { get; set; } = null!;
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
}

public class SetDefaultTierRequest
{
    public int TierId { get; set; }
    public int ProductId { get; set; }
}

public class DeactivateTierRequest
{
    public int TierId { get; set; }
    public int ProductId { get; set; }
}

public class ReactivateTierRequest
{
    public int TierId { get; set; }
    public int ProductId { get; set; }
}

public class ProductTierSelectionResponse
{
    public bool HasTiers { get; set; }
    public int? DefaultTierId { get; set; }
    public string CurrencySymbol { get; set; } = "€";
    public List<TierOption> Tiers { get; set; } = new();
}

public class TierOption
{
    public int Id { get; set; }
    public string TierName { get; set; } = null!;
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public bool IsDefault { get; set; }
}
