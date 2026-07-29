using Microsoft.AspNetCore.DataProtection;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Implements Stripe key encryption using ASP.NET Core Data Protection with purpose "StripeApiKeys.v1".
/// </summary>
public class StripeKeyEncryptionService : IStripeKeyEncryptionService
{
    private readonly IDataProtector _protector;

    public StripeKeyEncryptionService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("StripeApiKeys.v1");
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentException("Value to encrypt cannot be null or empty.", nameof(plainText));

        return _protector.Protect(plainText);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentException("Value to decrypt cannot be null or empty.", nameof(cipherText));

        return _protector.Unprotect(cipherText);
    }

    public string Mask(string decryptedValue)
    {
        if (string.IsNullOrEmpty(decryptedValue))
            return "****";

        // For keys like "sk_test_abc123xyz456"
        // Show prefix up to and including the second underscore, then mask, then last 4
        var underscoreCount = 0;
        var prefixEnd = 0;
        for (int i = 0; i < decryptedValue.Length && underscoreCount < 2; i++)
        {
            if (decryptedValue[i] == '_')
                underscoreCount++;
            prefixEnd = i + 1;
        }

        // If key is too short to mask meaningfully, just show first 4 + last 4
        if (decryptedValue.Length <= 8)
            return decryptedValue[..Math.Min(4, decryptedValue.Length)] + "****";

        var prefix = underscoreCount >= 2
            ? decryptedValue[..prefixEnd]
            : decryptedValue[..Math.Min(4, decryptedValue.Length)];
        var suffix = decryptedValue[^4..];

        return $"{prefix}****...{suffix}";
    }
}
