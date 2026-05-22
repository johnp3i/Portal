namespace Portal.Infrastructure.Entities;

/// <summary>
/// A classification for Purchase entries.
/// Schema: [purchase].ExpenseCategory
/// </summary>
public class ExpenseCategory
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}
