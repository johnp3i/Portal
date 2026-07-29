namespace Portal.Web.Models;

public class SaveStripeKeysRequest
{
    public string? ConnectClientId { get; set; }
    public string? SecretKey { get; set; }
    public string? WebhookSecret { get; set; }
}

public class RevealStripeKeyRequest
{
    public string KeyType { get; set; } = null!;
}
