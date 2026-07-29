namespace Portal.Web.Services.Stripe;

/// <summary>
/// Encrypts, decrypts, and masks Stripe API key values using ASP.NET Core Data Protection.
/// </summary>
public interface IStripeKeyEncryptionService
{
    /// <summary>Encrypts a plaintext key value for database storage.</summary>
    string Encrypt(string plainText);

    /// <summary>Decrypts a stored ciphertext back to the original key value.</summary>
    string Decrypt(string cipherText);

    /// <summary>Returns a masked representation showing prefix and last 4 characters (e.g., sk_test_****...Hf4K).</summary>
    string Mask(string decryptedValue);
}
