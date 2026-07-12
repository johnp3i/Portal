namespace Portal.Infrastructure.Models.Import;

/// <summary>
/// A parsed row after validation, with status and error/warning messages.
/// </summary>
public class ValidatedRow
{
    public ParsedRow Data { get; set; } = null!;

    public RowValidationStatus Status { get; set; }

    public List<string> Errors { get; set; } = new();

    public List<string> Warnings { get; set; } = new();

    public bool IsDuplicate { get; set; }

    public bool IsRemoved { get; set; }
}

/// <summary>
/// Validation status for a parsed import row.
/// </summary>
public enum RowValidationStatus
{
    Valid,
    Warning,
    Invalid
}
