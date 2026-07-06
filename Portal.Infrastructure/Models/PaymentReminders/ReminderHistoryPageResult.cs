namespace Portal.Infrastructure.Models.PaymentReminders;

/// <summary>
/// Paginated result for the reminder history query.
/// </summary>
public class ReminderHistoryPageResult
{
    public List<ReminderHistoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
