namespace Portal.Infrastructure.Models.PaymentReminders;

/// <summary>
/// Projected upcoming reminder (read-only, not yet sent).
/// </summary>
public class UpcomingReminderDto
{
    public DateOnly ScheduledDate { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string EscalationTier { get; set; } = null!;
    public decimal OutstandingAmount { get; set; }
    public DateOnly DueDate { get; set; }
}
