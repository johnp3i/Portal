namespace Portal.Infrastructure.Models.Import;

/// <summary>
/// A single column mapping entry within a parser template.
/// </summary>
public class ColumnMapping
{
    /// <summary>Header name (null if using positional index).</summary>
    public string? SourceColumn { get; set; }

    /// <summary>Zero-based positional index (null if using header name).</summary>
    public int? SourceIndex { get; set; }

    /// <summary>Target purchase field (e.g., "InvoiceDate").</summary>
    public string TargetField { get; set; } = null!;

    /// <summary>Date pattern or decimal separator ("." or ",").</summary>
    public string? Format { get; set; }

    /// <summary>Whether to skip this column during import.</summary>
    public bool IsSkipped { get; set; }
}
