namespace Portal.Infrastructure.Services;

/// <summary>
/// Result of a pre-upload storage-cap check.
/// <list type="bullet">
/// <item><see cref="Allowed"/> false → hard block; <see cref="Message"/> is the user-facing reason.</item>
/// <item><see cref="Allowed"/> true with a non-null <see cref="Warning"/> → the upload is permitted
/// but pushes the business to ≥80% of its cap; callers may surface the warning (advisory only).</item>
/// </list>
/// </summary>
public sealed record StorageCheckResult(bool Allowed, string? Message, string? Warning = null)
{
    /// <summary>Permitted, no warning.</summary>
    public static StorageCheckResult Ok() => new(true, null);

    /// <summary>Permitted, but nearing the cap — <paramref name="warning"/> is advisory (non-blocking).</summary>
    public static StorageCheckResult OkWithWarning(string warning) => new(true, null, warning);

    /// <summary>Hard-blocked — <paramref name="message"/> explains why.</summary>
    public static StorageCheckResult Blocked(string message) => new(false, message);
}

/// <summary>
/// Phase 3 storage enforcement. A single shared, reusable gate every upload path calls before
/// persisting a file. Hard-blocks an upload that would push a business over its plan storage cap.
/// Businesses on an unlimited plan (StorageLimitMb NULL/0) are never blocked.
/// </summary>
public interface IStorageLimitEnforcer
{
    /// <summary>
    /// Checks whether a business may upload a file of <paramref name="incomingBytes"/>:
    /// <list type="bullet">
    /// <item>unlimited plan or comfortably within the cap → <see cref="StorageCheckResult.Ok"/>;</item>
    /// <item>permitted but the upload lands at ≥80% of the cap → <see cref="StorageCheckResult.OkWithWarning"/>;</item>
    /// <item>would exceed the cap → <see cref="StorageCheckResult.Blocked"/> with a formatted message.</item>
    /// </list>
    /// </summary>
    Task<StorageCheckResult> CheckCanUploadAsync(int businessId, long incomingBytes);
}
