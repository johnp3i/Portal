namespace Portal.Infrastructure.Entities;

/// <summary>
/// Reference table defining accepted payment methods.
/// Schema: [revenue].PaymentMethodType
/// Seed values: Cash (1), BankTransfer (2), Card (3), Cheque (4), Other (5)
/// </summary>
public class PaymentMethodType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    // Navigation properties
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
