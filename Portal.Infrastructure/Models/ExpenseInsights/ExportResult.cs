namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// Result model for CSV export operations containing the file content and metadata.
/// </summary>
public class ExportResult
{
    public byte[] Content { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
}
