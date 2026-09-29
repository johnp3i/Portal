using Moq;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="StorageLimitEnforcer"/> — the shared upload gate (Phase 3, extended in
/// Phase 4a to cover signatures). The enforcer is source-agnostic: it trusts whatever
/// <see cref="IStorageUsageService.GetUsageAndLimitAsync"/> returns, so mocking that pair fully
/// exercises the block/warn/allow decision without touching the DB or filesystem.
///
/// Boundary reminder (matches StorageThresholds.IsOver = used &gt;= limit): a projected total that
/// EQUALS the cap is treated as over and blocked.
/// </summary>
public class StorageLimitEnforcerTests
{
    private const int TestBusinessId = 1;

    private readonly Mock<IStorageUsageService> _usageMock;
    private readonly StorageLimitEnforcer _enforcer;

    public StorageLimitEnforcerTests()
    {
        _usageMock = new Mock<IStorageUsageService>();
        _enforcer = new StorageLimitEnforcer(_usageMock.Object);
    }

    private void SetupUsage(long usedBytes, long? limitBytes)
    {
        _usageMock
            .Setup(s => s.GetUsageAndLimitAsync(TestBusinessId))
            .ReturnsAsync((usedBytes, limitBytes));
    }

    // ── Unlimited plans are never blocked or warned ──────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public async Task Unlimited_AlwaysAllowed_NoWarning(long? limit)
    {
        SetupUsage(usedBytes: 10_000, limitBytes: limit);

        var result = await _enforcer.CheckCanUploadAsync(TestBusinessId, 5_000);

        Assert.True(result.Allowed);
        Assert.Null(result.Warning);
        Assert.Null(result.Message);
    }

    // ── Comfortably within the cap → allowed, no warning ─────────────────────

    [Fact]
    public async Task WellUnderCap_Allowed_NoWarning()
    {
        // used 10 + incoming 10 = 20 of 100 = 20%
        SetupUsage(usedBytes: 10, limitBytes: 100);

        var result = await _enforcer.CheckCanUploadAsync(TestBusinessId, 10);

        Assert.True(result.Allowed);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task JustBelowWarnThreshold_Allowed_NoWarning()
    {
        // used 70 + incoming 9 = 79 of 100 = 79% → below 80, no warning
        SetupUsage(usedBytes: 70, limitBytes: 100);

        var result = await _enforcer.CheckCanUploadAsync(TestBusinessId, 9);

        Assert.True(result.Allowed);
        Assert.Null(result.Warning);
    }

    // ── Lands 80–99% → allowed WITH advisory warning ─────────────────────────

    [Fact]
    public async Task LandsAtWarnThreshold_Allowed_WithWarning()
    {
        // used 70 + incoming 10 = 80 of 100 = 80% → warn
        SetupUsage(usedBytes: 70, limitBytes: 100);

        var result = await _enforcer.CheckCanUploadAsync(TestBusinessId, 10);

        Assert.True(result.Allowed);
        Assert.NotNull(result.Warning);
        Assert.Contains("80%", result.Warning!);
    }

    [Fact]
    public async Task LandsNearCapButUnder_Allowed_WithWarning()
    {
        // used 90 + incoming 9 = 99 of 100 = 99% → warn, not blocked
        SetupUsage(usedBytes: 90, limitBytes: 100);

        var result = await _enforcer.CheckCanUploadAsync(TestBusinessId, 9);

        Assert.True(result.Allowed);
        Assert.NotNull(result.Warning);
    }

    // ── At or over the cap → hard block ──────────────────────────────────────

    [Fact]
    public async Task ProjectedExactlyAtCap_Blocked()
    {
        // used 90 + incoming 10 = 100 of 100 → IsOver (>=) → blocked
        SetupUsage(usedBytes: 90, limitBytes: 100);

        var result = await _enforcer.CheckCanUploadAsync(TestBusinessId, 10);

        Assert.False(result.Allowed);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task ProjectedOverCap_Blocked_MessageHasRemainingAndFileSize()
    {
        // used 900 KB, cap 1 MB (1024 KB), incoming 500 KB → over
        const long kb = 1024L;
        SetupUsage(usedBytes: 900 * kb, limitBytes: 1024 * kb);

        var result = await _enforcer.CheckCanUploadAsync(TestBusinessId, 500 * kb);

        Assert.False(result.Allowed);
        Assert.NotNull(result.Message);
        // Message leads with remaining free space (1024-900 = 124 KB) and the file size (500 KB).
        Assert.Contains("124", result.Message!);
        Assert.Contains("500", result.Message!);
        Assert.Contains("Storage limit reached", result.Message!);
    }

    [Fact]
    public async Task AlreadyOverCap_ZeroByteUpload_StillBlocked()
    {
        // Already over (110 of 100); even a 0-byte upload keeps it over.
        SetupUsage(usedBytes: 110, limitBytes: 100);

        var result = await _enforcer.CheckCanUploadAsync(TestBusinessId, 0);

        Assert.False(result.Allowed);
    }

    // ── Negative incoming is clamped to 0 ────────────────────────────────────

    [Fact]
    public async Task NegativeIncoming_ClampedToZero_NotBlockedWhenUnderCap()
    {
        // used 50 of 100; negative incoming treated as 0 → 50% → allowed, no warning
        SetupUsage(usedBytes: 50, limitBytes: 100);

        var result = await _enforcer.CheckCanUploadAsync(TestBusinessId, -999);

        Assert.True(result.Allowed);
        Assert.Null(result.Warning);
    }

    // ── Source-agnostic: signatures counted in "used" push over the cap ──────
    // (Phase 4a folds signature bytes into GetUsageAndLimitAsync's used total; the enforcer only
    //  sees the higher number and blocks accordingly — proven by a used value that already includes
    //  signature bytes.)

    [Fact]
    public async Task UsageIncludingSignatures_OverCap_Blocked()
    {
        // e.g. attachments+logos = 80, signatures = 25 → used 105 of 100 already over.
        SetupUsage(usedBytes: 105, limitBytes: 100);

        var result = await _enforcer.CheckCanUploadAsync(TestBusinessId, 1);

        Assert.False(result.Allowed);
    }
}
