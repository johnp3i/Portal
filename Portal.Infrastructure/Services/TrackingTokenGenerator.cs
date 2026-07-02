using System.Security.Cryptography;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Generates cryptographically secure tracking tokens for email open tracking.
/// Produces URL-safe Base64-encoded tokens from 32 bytes of entropy.
/// </summary>
public static class TrackingTokenGenerator
{
    private const int TokenByteLength = 32;

    /// <summary>
    /// Generates a new URL-safe Base64-encoded tracking token (32 bytes of entropy).
    /// </summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
