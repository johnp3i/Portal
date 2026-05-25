namespace Portal.Infrastructure.Models;

/// <summary>
/// Represents a historical record of a customer statement email that was sent.
/// </summary>
public class StatementEmailHistoryDto
{
    public DateTime SentAtUtc { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string SentByDisplayName { get; set; } = string.Empty;
}
