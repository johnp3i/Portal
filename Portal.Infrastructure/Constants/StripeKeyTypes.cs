namespace Portal.Infrastructure.Constants;

public static class StripeKeyTypes
{
    public const string ConnectClientId = "connect_client_id";
    public const string SecretKey = "secret_key";
    public const string WebhookSecret = "webhook_secret";

    public static readonly string[] All = { ConnectClientId, SecretKey, WebhookSecret };

    public static bool IsValid(string keyType) => keyType is not null && All.Contains(keyType);
}
