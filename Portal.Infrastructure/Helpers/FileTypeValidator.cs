namespace Portal.Infrastructure.Helpers;

/// <summary>
/// Validates uploaded files against allowed types using three-layer verification:
/// extension check, Content-Type check, and magic-byte verification.
/// </summary>
public static class FileTypeValidator
{
    private static readonly Dictionary<string, string[]> AllowedExtensionContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".pdf", new[] { "application/pdf" } },
        { ".png", new[] { "image/png" } },
        { ".jpg", new[] { "image/jpeg" } },
        { ".jpeg", new[] { "image/jpeg" } },
        { ".webp", new[] { "image/webp" } }
    };

    private static readonly Dictionary<string, byte[][]> MagicBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "application/pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D } } },                     // %PDF-
        { "image/png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },        // PNG header
        { "image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },                                       // JPEG SOI
        { "image/webp", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } }                                  // RIFF (WEBP starts with RIFF....WEBP)
    };

    // WEBP has additional signature at offset 8
    private static readonly byte[] WebpSignature = { 0x57, 0x45, 0x42, 0x50 }; // "WEBP"

    /// <summary>
    /// Validates a file against extension, Content-Type, and magic-byte rules.
    /// Stream position is reset after validation.
    /// </summary>
    public static FileValidationResult Validate(string fileName, string contentType, Stream fileStream)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return FileValidationResult.Fail("File name is required.");

        if (string.IsNullOrWhiteSpace(contentType))
            return FileValidationResult.Fail("Content type is required.");

        // Layer 1: Extension check
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensionContentTypes.ContainsKey(extension))
        {
            return FileValidationResult.Fail("File type not allowed. Accepted: PDF, PNG, JPG, WEBP.");
        }

        // Layer 2: Content-Type check
        var allowedContentTypes = AllowedExtensionContentTypes[extension];
        if (!allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return FileValidationResult.Fail("File extension does not match content type.");
        }

        // Additional consistency: verify declared Content-Type is one of our known types
        if (!MagicBytes.ContainsKey(contentType))
        {
            return FileValidationResult.Fail("File type not allowed. Accepted: PDF, PNG, JPG, WEBP.");
        }

        // Layer 3: Magic-byte verification
        var originalPosition = fileStream.Position;
        try
        {
            fileStream.Position = 0;

            var expectedSignatures = MagicBytes[contentType];
            var maxLength = expectedSignatures.Max(s => s.Length);

            // For WEBP, we need at least 12 bytes (RIFF + 4 bytes size + WEBP)
            if (contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
                maxLength = 12;

            var headerBytes = new byte[maxLength];
            var bytesRead = fileStream.Read(headerBytes, 0, maxLength);

            if (bytesRead < expectedSignatures.Min(s => s.Length))
            {
                return FileValidationResult.Fail("File content does not match the declared file type.");
            }

            var startsWithSignature = expectedSignatures.Any(sig =>
                bytesRead >= sig.Length && headerBytes.Take(sig.Length).SequenceEqual(sig));

            if (!startsWithSignature)
            {
                return FileValidationResult.Fail("File content does not match the declared file type.");
            }

            // Additional WEBP check: bytes 8-11 must be "WEBP"
            if (contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
            {
                if (bytesRead < 12 || !headerBytes.Skip(8).Take(4).SequenceEqual(WebpSignature))
                {
                    return FileValidationResult.Fail("File content does not match the declared file type.");
                }
            }

            return FileValidationResult.Ok();
        }
        finally
        {
            fileStream.Position = originalPosition;
        }
    }
}

/// <summary>
/// Result of a file type validation operation.
/// </summary>
public class FileValidationResult
{
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static FileValidationResult Ok() => new() { IsValid = true };
    public static FileValidationResult Fail(string message) => new() { IsValid = false, ErrorMessage = message };
}
