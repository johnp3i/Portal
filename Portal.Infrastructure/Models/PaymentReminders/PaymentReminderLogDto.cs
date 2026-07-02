namespace Portal.Infrastructure.Models.PaymentReminders;

public class PaymentReminderLogDto
{
    public string EscalationTier { get; set; } = null!;
    public string RecipientEmail { get; set; } = null!;
    public DateTime SentAtUtc { get; set; }
    public bool IsManualTrigger { get; set; }
    public bool IsSentSuccessfully { get; set; }
    public string? ErrorMessage { get; set; }

    // --- Open Tracking ---
    public bool IsOpened { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public int OpenCount { get; set; }
    public DateTime? LastOpenedAtUtc { get; set; }

    // --- Test Send Flag ---
    public bool IsTestSend { get; set; }
}
