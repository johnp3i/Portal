namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure, side-effect-free matcher that decides whether a physical file (identified by its relative
/// path under the storage root) is still referenced by a live DB row. The filesystem walk and the
/// DB fetch live in <see cref="IOrphanedFileCleanupService"/>; this type only compares normalized
/// paths, so it is fully unit-testable and carries the single most safety-critical rule of the
/// cleanup feature: a wrong "orphaned" verdict would delete a live file.
///
/// The referenced-path set is assembled by the caller from every file-owning table, each mapped to
/// the SAME relative-path form used on disk:
/// <list type="bullet">
/// <item>Document attachments — <c>DocumentAttachment.StoragePath</c> verbatim (ALL rows, incl.
/// soft-deleted <c>IsDeleted = 1</c>, which still reference their file).</item>
/// <item>Compliance attachments — <c>ApplicationAttachment.FilePath</c> verbatim.</item>
/// <item>Logos — reconstructed as <c>{BusinessId}/logos/{FileName}</c> (the DB stores only the bare
/// filename; the on-disk folder is "logos").</item>
/// <item>Signatures — <c>Signature.FilePath</c> verbatim (ALL rows, incl. inactive).</item>
/// </list>
/// </summary>
public static class OrphanedFileMatcher
{
    /// <summary>
    /// Normalizes a storage path for comparison: backslashes → forward slashes, trimmed of leading
    /// slashes/whitespace. Case is preserved here; callers build the set with a case-insensitive
    /// comparer (Windows filesystem is case-insensitive), so matching is done by the set's comparer.
    /// </summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        return path.Replace('\\', '/').Trim().TrimStart('/');
    }

    /// <summary>
    /// Builds the comparison set for referenced paths. Uses <see cref="StringComparer.OrdinalIgnoreCase"/>
    /// so disk paths that differ only by case (Windows) still match a referenced row.
    /// </summary>
    public static HashSet<string> BuildReferencedSet(IEnumerable<string> referencedRelativePaths)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in referencedRelativePaths)
        {
            var norm = Normalize(p);
            if (norm.Length > 0) set.Add(norm);
        }
        return set;
    }

    /// <summary>
    /// True when the file at <paramref name="relativePath"/> is referenced by a live DB row (i.e.
    /// it is NOT an orphan). The <paramref name="referencedPaths"/> set must be built with a
    /// case-insensitive comparer (use <see cref="BuildReferencedSet"/>).
    /// </summary>
    public static bool IsReferenced(string? relativePath, HashSet<string> referencedPaths)
    {
        var norm = Normalize(relativePath);
        if (norm.Length == 0) return true; // empty/garbage path → treat as referenced (never delete)
        return referencedPaths.Contains(norm);
    }

    /// <summary>Convenience inverse of <see cref="IsReferenced"/>.</summary>
    public static bool IsOrphaned(string? relativePath, HashSet<string> referencedPaths) =>
        !IsReferenced(relativePath, referencedPaths);

    /// <summary>Reconstructs a logo's on-disk relative path from its business id + bare filename.</summary>
    public static string LogoRelativePath(int businessId, string fileName) =>
        Normalize($"{businessId}/logos/{fileName}");
}
