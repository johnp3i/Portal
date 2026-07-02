namespace Portal.Infrastructure.Models.PaymentReminders;

public class ReminderDashboardWidgetDto
{
    public int TotalRemindersSentThisWeek { get; set; }
    public int PaymentsReceivedAfterReminder { get; set; }
    public decimal AmountReceivedAfterReminder { get; set; }
}
