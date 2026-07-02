namespace Portal.Infrastructure.Models.PaymentReminders;

/// <summary>
/// Result of sending a test reminder email.
/// </summary>
public class TestReminderResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
