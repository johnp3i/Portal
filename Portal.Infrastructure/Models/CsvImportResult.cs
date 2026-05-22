namespace Portal.Infrastructure.Models;

/// <summary>
/// Result of a CSV import parse operation, containing either parsed rows or a file-level error.
/// </summary>
public class CsvImportResult
{
    /// <summary>
    /// Whether the file was successfully parsed (individual rows may still have validation errors).
    /// </summary>
    public bool IsFileValid { get; set; }

    /// <summary>
    /// File-level error message (e.g., exceeds 500 rows, malformed CSV).
    /// Null when IsFileValid is true.
    /// </summary>
    public string? FileError { get; set; }

    /// <summary>
    /// Parsed and validated rows. Empty if IsFileValid is false.
    /// </summary>
    public List<CsvPurchaseRowDto> Rows { get; set; } = new();

    public static CsvImportResult Success(List<CsvPurchaseRowDto> rows) =>
        new() { IsFileValid = true, Rows = rows };

    public static CsvImportResult Fail(string error) =>
        new() { IsFileValid = false, FileError = error };
}
