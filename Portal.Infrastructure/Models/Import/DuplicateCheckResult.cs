namespace Portal.Infrastructure.Models.Import;

/// <summary>
/// Result of checking a single import row for potential duplicates.
/// </summary>
public class DuplicateCheckResult
{
    public int RowIndex { get; set; }

    public bool IsDuplicate { get; set; }

    public int? MatchedPurchaseId { get; set; }
}
