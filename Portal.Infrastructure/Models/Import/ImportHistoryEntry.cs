namespace Portal.Infrastructure.Models.Import;

/// <summary>
/// Lightweight DTO for displaying import history on the Upload page.
/// </summary>
public class ImportHistoryEntry
{
    public string? UserId { get; set; }
    public string? RecordId { get; set; }
    public string? NewValues { get; set; }
    public DateTime Timestamp { get; set; }
}
