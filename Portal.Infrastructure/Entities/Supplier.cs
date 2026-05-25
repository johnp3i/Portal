namespace Portal.Infrastructure.Entities;

/// <summary>
/// A vendor entity from whom Purchases are made.
/// Schema: [purchase].Supplier
/// </summary>
public class Supplier
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
