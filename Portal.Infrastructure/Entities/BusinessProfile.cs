namespace Portal.Infrastructure.Entities;

/// <summary>
/// Configuration record holding company registration, VAT details, and contact information for a Business.
/// Schema: [portal].BusinessProfile
/// </summary>
public class BusinessProfile
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string CompanyRegistrationNumber { get; set; } = null!;

    public string VatRegistrationNumber { get; set; } = null!;

    public DateOnly VatRegistrationDate { get; set; }

    public int VatPeriodLengthInMonths { get; set; }

    public string AddressLine1 { get; set; } = null!;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = null!;

    public string PostalCode { get; set; } = null!;

    public string Country { get; set; } = null!;

    public string? TelephoneNumber { get; set; }

    public string? MobileNumber { get; set; }

    public string Email { get; set; } = null!;

    public string? Website { get; set; }

    public string CurrencySymbol { get; set; } = "€";

    // Navigation properties
    public Business Business { get; set; } = null!;
}
