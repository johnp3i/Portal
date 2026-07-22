namespace Portal.Infrastructure.Entities;

/// <summary>
/// A client entity registered under a specific Business tenant.
/// Schema: [customer].Customer
/// </summary>
public class Customer
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Name { get; set; } = null!;

    public string? ContactPerson { get; set; }

    public string? Email { get; set; }

    public string? TelephoneNumber { get; set; }

    public string? MobileNumber { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public bool IsActive { get; set; }

    public bool IsReminderOptedOut { get; set; }

    public int? ContactId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
