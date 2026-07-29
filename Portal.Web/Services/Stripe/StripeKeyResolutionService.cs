using Microsoft.Extensions.Options;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Repositories;
using Portal.Web.Configuration;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Resolves Stripe keys: per-business DB keys → platform User Secrets fallback.
/// Auto-generates the OAuth redirect URI from the current domain.
/// </summary>
public class StripeKeyResolutionService : IStripeKeyResolutionService
{
    private readonly BusinessApiKeysRepository _repository;
    private readonly IStripeKeyEncryptionService _encryptionService;
    private readonly StripeSettings _platformSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StripeKeyResolutionService(
        BusinessApiKeysRepository repository,
        IStripeKeyEncryptionService encryptionService,
        IOptions<StripeSettings> platformSettings,
        IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _encryptionService = encryptionService;
        _platformSettings = platformSettings.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ResolvedStripeKeys> ResolveKeysAsync(int businessId)
    {
        var result = new ResolvedStripeKeys();

        // 1. Try per-business DB keys
        var dbKeys = await _repository.GetByBusinessIdAsync(businessId);
        if (dbKeys.Count > 0)
        {
            foreach (var key in dbKeys)
            {
                var decrypted = _encryptionService.Decrypt(key.EncryptedValue);
                switch (key.KeyType)
                {
                    case StripeKeyTypes.ConnectClientId:
                        result.ConnectClientId = decrypted;
                        break;
                    case StripeKeyTypes.SecretKey:
                        result.SecretKey = decrypted;
                        break;
                    case StripeKeyTypes.WebhookSecret:
                        result.ConnectWebhookSecret = decrypted;
                        break;
                }
            }

            // If all three DB keys are present, mark as from database
            if (result.IsComplete)
            {
                result.IsFromDatabase = true;
            }
            else
            {
                // Partial DB keys — fall back to platform for missing ones
                result.ConnectClientId ??= _platformSettings.ConnectClientId;
                result.SecretKey ??= _platformSettings.SecretKey;
                result.ConnectWebhookSecret ??= _platformSettings.ConnectWebhookSecret;
                result.IsFromDatabase = true; // At least some keys came from DB
            }
        }
        else
        {
            // 2. Fall back to platform User Secrets
            result.ConnectClientId = _platformSettings.ConnectClientId;
            result.SecretKey = _platformSettings.SecretKey;
            result.ConnectWebhookSecret = _platformSettings.ConnectWebhookSecret;
            result.IsFromDatabase = false;
        }

        // 3. Always auto-generate redirect URI from current domain
        result.ConnectOAuthRedirectUri = GenerateRedirectUri();

        return result;
    }

    public async Task<bool> HasBusinessKeysAsync(int businessId)
    {
        var keys = await _repository.GetByBusinessIdAsync(businessId);
        return keys.Count > 0;
    }

    private string GenerateRedirectUri()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
            return _platformSettings.ConnectOAuthRedirectUri ?? "";

        var scheme = request.Scheme;
        var host = request.Host.ToString();
        return $"{scheme}://{host}/MyBusiness/StripeConnectCallback";
    }
}
