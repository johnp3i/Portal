using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="OrphanedFileMatcher"/> — the safety-critical core of the orphaned-file
/// cleanup. A wrong "orphaned" verdict would delete a live file, so these tests pin down the exact
/// referenced-vs-orphaned rules, path normalization, and the per-convention reconstruction (esp.
/// logos, whose DB row stores only a bare filename).
/// </summary>
public class OrphanedFileMatcherTests
{
    // ── Normalize ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1/Purchase/5/abc_file.pdf", "1/Purchase/5/abc_file.pdf")]
    [InlineData("1\\Purchase\\5\\abc_file.pdf", "1/Purchase/5/abc_file.pdf")] // backslashes → forward
    [InlineData("/1/logos/x.png", "1/logos/x.png")]                          // leading slash trimmed
    [InlineData("  1/logos/x.png  ", "1/logos/x.png")]                       // trimmed
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_MapsToCanonicalRelativePath(string? input, string expected)
    {
        Assert.Equal(expected, OrphanedFileMatcher.Normalize(input));
    }

    // ── Referenced vs orphaned ───────────────────────────────────────────────

    private static HashSet<string> Referenced(params string[] paths) =>
        OrphanedFileMatcher.BuildReferencedSet(paths);

    [Fact]
    public void DocumentAttachmentPath_InSet_IsReferenced()
    {
        var set = Referenced("1/Purchase/5/guid_invoice.pdf");
        Assert.True(OrphanedFileMatcher.IsReferenced("1/Purchase/5/guid_invoice.pdf", set));
        Assert.False(OrphanedFileMatcher.IsOrphaned("1/Purchase/5/guid_invoice.pdf", set));
    }

    [Fact]
    public void CompliancePath_InSet_IsReferenced()
    {
        var set = Referenced("1/compliance/9/guid_filing.pdf");
        Assert.True(OrphanedFileMatcher.IsReferenced("1/compliance/9/guid_filing.pdf", set));
    }

    [Fact]
    public void SignaturePath_InSet_IsReferenced()
    {
        var set = Referenced("signatures/1/guid_sig.png");
        Assert.True(OrphanedFileMatcher.IsReferenced("signatures/1/guid_sig.png", set));
    }

    [Fact]
    public void LogoPath_ReconstructedFromBusinessAndFileName_IsReferenced()
    {
        // DB stores only the bare filename; the matcher/service reconstructs {businessId}/logos/{file}.
        var logoRel = OrphanedFileMatcher.LogoRelativePath(1, "abc123.png");
        Assert.Equal("1/logos/abc123.png", logoRel);

        var set = Referenced(logoRel);
        Assert.True(OrphanedFileMatcher.IsReferenced("1/logos/abc123.png", set));
    }

    [Fact]
    public void PathPresentInSet_IsReferenced_NotOrphaned()
    {
        // Whatever the caller put in the set is, by definition, referenced from the matcher's view.
        var set = Referenced("1/Invoice/3/guid_live.pdf");
        Assert.False(OrphanedFileMatcher.IsOrphaned("1/Invoice/3/guid_live.pdf", set));
    }

    [Fact]
    public void UnknownPath_NotInSet_IsOrphaned()
    {
        var set = Referenced("1/Purchase/5/guid_invoice.pdf");
        Assert.True(OrphanedFileMatcher.IsOrphaned("1/Purchase/5/GHOST_leftover.pdf", set));
    }

    // ── Normalization affects matching ───────────────────────────────────────

    [Fact]
    public void BackslashDiskPath_MatchesForwardSlashReferenced()
    {
        var set = Referenced("1/Purchase/5/guid_invoice.pdf");
        // A disk walk on Windows may yield backslashes; must still match.
        Assert.True(OrphanedFileMatcher.IsReferenced(@"1\Purchase\5\guid_invoice.pdf", set));
    }

    [Fact]
    public void CaseDifference_StillMatches_OnCaseInsensitiveSet()
    {
        var set = Referenced("1/Logos/ABC.png");
        Assert.True(OrphanedFileMatcher.IsReferenced("1/logos/abc.png", set));
    }

    // ── Safety: empty/garbage paths are never treated as orphaned ────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrNullPath_IsTreatedAsReferenced_NeverDeleted(string? path)
    {
        var set = Referenced("1/Purchase/5/guid_invoice.pdf");
        Assert.True(OrphanedFileMatcher.IsReferenced(path, set));
        Assert.False(OrphanedFileMatcher.IsOrphaned(path, set));
    }

    [Fact]
    public void EmptyReferencedSet_RealPath_IsOrphaned()
    {
        var set = Referenced(); // nothing referenced
        Assert.True(OrphanedFileMatcher.IsOrphaned("1/Purchase/5/guid_invoice.pdf", set));
    }

    // ── Tombstone (soft-delete releases a file for grace-period cleanup) ──────

    [Fact]
    public void Tombstone_PrefixesThePath()
    {
        Assert.Equal("deleted/1/Purchase/5/guid_invoice.pdf",
            OrphanedFileMatcher.Tombstone("1/Purchase/5/guid_invoice.pdf"));
    }

    [Fact]
    public void Tombstone_IsIdempotent_NoDoublePrefix()
    {
        var once = OrphanedFileMatcher.Tombstone("1/Purchase/5/x.pdf");
        var twice = OrphanedFileMatcher.Tombstone(once);
        Assert.Equal(once, twice);
        Assert.Equal("deleted/1/Purchase/5/x.pdf", twice);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Tombstone_EmptyInput_StaysEmpty(string? input)
    {
        Assert.Equal(string.Empty, OrphanedFileMatcher.Tombstone(input));
    }

    [Fact]
    public void Tombstone_NormalizesBackslashesBeforePrefixing()
    {
        Assert.Equal("deleted/1/Purchase/5/x.pdf",
            OrphanedFileMatcher.Tombstone(@"1\Purchase\5\x.pdf"));
    }

    [Fact]
    public void IsTombstoned_DetectsPrefix_CaseInsensitive()
    {
        Assert.True(OrphanedFileMatcher.IsTombstoned("deleted/1/Purchase/5/x.pdf"));
        Assert.True(OrphanedFileMatcher.IsTombstoned("DELETED/1/Purchase/5/x.pdf"));
        Assert.False(OrphanedFileMatcher.IsTombstoned("1/Purchase/5/x.pdf"));
        Assert.False(OrphanedFileMatcher.IsTombstoned(null));
    }

    [Fact]
    public void TombstonedPath_NotMatchingRealDiskPath_MakesFileOrphaned()
    {
        // After soft-delete the DB row holds the tombstoned path, so the referenced set (built from
        // non-tombstoned rows) no longer contains the real on-disk path → the file is now an orphan
        // and enters the normal grace-period cleanup.
        var realPath = "1/Purchase/5/guid_invoice.pdf";
        var set = Referenced(OrphanedFileMatcher.Tombstone(realPath)); // only the tombstoned form is present
        Assert.True(OrphanedFileMatcher.IsOrphaned(realPath, set));
    }
}
