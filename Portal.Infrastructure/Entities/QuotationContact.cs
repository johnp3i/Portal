namespace Portal.Infrastructure.Entities;

/// <summary>
/// A reusable contact record representing a person who prepares quotations for a business.
/// Schema: [quotation].QuotationContact
/// </summary>
public class QuotationContact
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string? UserId { get; set; }

    public string Name { get; set; } = null!;

    public string? Email { get; set; }

    public string? TelephoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;
}
