namespace Portal.Infrastructure.Entities;

/// <summary>
/// Stores an encrypted Stripe API key for a business.
/// Schema: [stripe].BusinessApiKeys
/// </summary>
public class BusinessApiKey
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string KeyType { get; set; } = null!;
    public string EncryptedValue { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation
    public Business Business { get; set; } = null!;
}
