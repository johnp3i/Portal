using Portal.Infrastructure.Models.Storage;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for the shared <see cref="StorageThresholds"/> maths — the single source of truth
/// used by the per-business tab, SuperAdmin page, badge/banner, and the upload enforcer. Locks in
/// the consistency fixes: "over" is at-or-past the cap (&gt;=), "near" is ≥80% but not yet over,
/// percent is rounded, and unlimited (null/0 limit) never signals.
/// </summary>
public class StorageThresholdsTests
{
    // ── Percent ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(80, 100, 80)]
    [InlineData(100, 100, 100)]
    [InlineData(150, 100, 150)]   // over 100% is reported as-is (not capped here)
    public void Percent_ComputesRounded(long used, long limit, int expected)
    {
        Assert.Equal(expected, StorageThresholds.Percent(used, limit));
    }

    [Theory]
    [InlineData(1, 3, 33)]     // 33.33 → 33
    [InlineData(2, 3, 67)]     // 66.66 → 67
    [InlineData(995, 1000, 100)] // 99.5 → 100 (rounds up)
    public void Percent_RoundsToNearest(long used, long limit, int expected)
    {
        Assert.Equal(expected, StorageThresholds.Percent(used, limit));
    }

    [Theory]
    [InlineData(500, null)]   // unlimited
    [InlineData(500, 0L)]     // 0 = unlimited
    public void Percent_UnlimitedOrZero_IsZero(long used, long? limit)
    {
        Assert.Equal(0, StorageThresholds.Percent(used, limit));
    }

    // ── IsOver (>=) ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(99, 100, false)]
    [InlineData(100, 100, true)]   // exactly at the cap = over (the key >= boundary)
    [InlineData(101, 100, true)]
    public void IsOver_UsesAtOrPastBoundary(long used, long limit, bool expected)
    {
        Assert.Equal(expected, StorageThresholds.IsOver(used, limit));
    }

    [Theory]
    [InlineData(9999, null)]
    [InlineData(9999, 0L)]
    public void IsOver_UnlimitedOrZero_IsFalse(long used, long? limit)
    {
        Assert.False(StorageThresholds.IsOver(used, limit));
    }

    // ── IsNear (≥80% and not over) ───────────────────────────────────────────

    [Theory]
    [InlineData(79, 100, false)]  // below 80%
    [InlineData(80, 100, true)]   // exactly 80%
    [InlineData(99, 100, true)]   // near but not over
    [InlineData(100, 100, false)] // at cap → over, not "near"
    [InlineData(120, 100, false)] // over → not "near"
    public void IsNear_TrueOnlyBetween80AndCap(long used, long limit, bool expected)
    {
        Assert.Equal(expected, StorageThresholds.IsNear(used, limit));
    }

    [Theory]
    [InlineData(9999, null)]
    [InlineData(9999, 0L)]
    public void IsNear_UnlimitedOrZero_IsFalse(long used, long? limit)
    {
        Assert.False(StorageThresholds.IsNear(used, limit));
    }

    [Fact]
    public void WarnAtPercent_Is80()
    {
        Assert.Equal(80, StorageThresholds.WarnAtPercent);
    }
}
