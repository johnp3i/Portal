namespace Portal.Infrastructure.Entities;

/// <summary>
/// A bank account record for a business, displayed on invoice previews as payment instructions.
/// Schema: [portal].BusinessPaymentDetail
/// </summary>
public class BusinessPaymentDetail
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Label { get; set; } = null!;

    public string BankName { get; set; } = null!;

    public string Iban { get; set; } = null!;

    public string PayeeName { get; set; } = null!;

    public string? SwiftBic { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;
}
