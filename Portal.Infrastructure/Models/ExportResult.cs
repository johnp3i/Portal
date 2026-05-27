namespace Portal.Infrastructure.Models;

/// <summary>
/// Generic wrapper for export responses with a truncation flag indicating whether
/// the result set exceeded the maximum allowed export limit.
/// </summary>
public class ExportResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public bool IsTruncated { get; set; }
}
