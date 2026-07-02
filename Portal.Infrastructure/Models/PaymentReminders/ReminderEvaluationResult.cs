namespace Portal.Infrastructure.Models.PaymentReminders;

public class ReminderEvaluationResult
{
    public int InvoicesEvaluated { get; set; }
    public int RemindersSent { get; set; }
    public int RemindersFailed { get; set; }
}
