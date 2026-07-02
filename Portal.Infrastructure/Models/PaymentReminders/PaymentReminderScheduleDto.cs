namespace Portal.Infrastructure.Models.PaymentReminders;

public class PaymentReminderScheduleDto
{
    public string EscalationTier { get; set; } = null!;
    public int DaysOffset { get; set; }
    public int MaxRemindersPerTier { get; set; }
    public int MinIntervalDays { get; set; }
    public int PartialPaymentSuppressionDays { get; set; }
    public bool IsEnabled { get; set; }
}
