namespace Portal.Infrastructure.Models.PaymentReminders;

/// <summary>
/// A single reminder log entry for the paginated history page.
/// </summary>
public class ReminderHistoryItemDto
{
    public int Id { get; set; }
    public DateTime SentAtUtc { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string EscalationTier { get; set; } = null!;
    public string RecipientEmail { get; set; } = null!;
    public bool IsManualTrigger { get; set; }
    public bool IsTestSend { get; set; }
    public bool IsSentSuccessfully { get; set; }
    public bool IsOpened { get; set; }
}
