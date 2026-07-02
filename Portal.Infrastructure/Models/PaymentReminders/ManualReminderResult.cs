namespace Portal.Infrastructure.Models.PaymentReminders;

public class ManualReminderResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool CustomerOptedOut { get; set; }
}
