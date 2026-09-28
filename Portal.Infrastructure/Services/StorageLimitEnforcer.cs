using Portal.Infrastructure.Models.Storage;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Default <see cref="IStorageLimitEnforcer"/>. Resolves current usage + plan cap via
/// <see cref="IStorageUsageService.GetUsageAndLimitAsync"/> and hard-blocks any upload that would
/// exceed the cap. Unlimited plans (no cap) always pass.
/// </summary>
public class StorageLimitEnforcer : IStorageLimitEnforcer
{
    private readonly IStorageUsageService _storageUsageService;

    public StorageLimitEnforcer(IStorageUsageService storageUsageService)
    {
        _storageUsageService = storageUsageService;
    }

    /// <summary>Percentage of the cap at (or above) which a soft warning is emitted.</summary>
    private const int WarnAtPercent = 80;

    public async Task<StorageCheckResult> CheckCanUploadAsync(int businessId, long incomingBytes)
    {
        try
        {
            var (usedBytes, limitBytes) = await _storageUsageService.GetUsageAndLimitAsync(businessId);

            // No cap set (unlimited plan) → always allowed, no warning.
            if (!limitBytes.HasValue || limitBytes.Value <= 0)
                return StorageCheckResult.Ok();

            var limit = limitBytes.Value;
            var incoming = incomingBytes < 0 ? 0 : incomingBytes;

            // NOTE (accepted concurrency tradeoff): this is a check-then-write with no locking.
            // Two uploads racing near the cap can both pass here and jointly exceed the limit by
            // up to one file each. This is deliberate for Phase 3 — the small, bounded overage is
            // preferred over the complexity/contention of row locks or a reservation table. Do not
            // assume this method is atomic with the subsequent persist.
            var projected = usedBytes + incoming;

            if (projected > limit)
            {
                // Over the cap — hard block. Lead with what's FREE (the actionable number) rather
                // than "used X of Y", which can look like "250 of 250" from rounding and confuse.
                var remaining = Math.Max(0, limit - usedBytes);
                var message =
                    $"Storage limit reached — only {StorageFormat.Bytes(remaining)} free of your " +
                    $"{StorageFormat.Bytes(limit)} plan storage, and this file is {StorageFormat.Bytes(incoming)}. " +
                    $"Delete some files to free up space, or upgrade your plan to upload more.";
                return StorageCheckResult.Blocked(message);
            }

            // Permitted. If this upload lands the business at ≥80% of the cap, attach an advisory
            // warning so callers can nudge the user before they hit the wall next time.
            var projectedPercent = (int)(projected * 100L / limit);
            if (projectedPercent >= WarnAtPercent)
            {
                var remaining = Math.Max(0, limit - projected);
                var warning =
                    $"Heads up — you're at {projectedPercent}% of your {StorageFormat.Bytes(limit)} " +
                    $"storage after this upload ({StorageFormat.Bytes(remaining)} left). " +
                    $"Consider deleting old files or upgrading your plan soon.";
                return StorageCheckResult.OkWithWarning(warning);
            }

            return StorageCheckResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
