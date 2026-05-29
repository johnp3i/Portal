namespace Portal.Infrastructure.Entities;

/// <summary>
/// Lookup table classifying the type of a Product.
/// Schema: [product].ProductType
/// Values: Services (1), Goods (2)
/// </summary>
public class ProductType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
