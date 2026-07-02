namespace Portal.Infrastructure.Entities;

/// <summary>
/// Audit record for each reminder email sent (or failed).
/// Schema: [reminder].PaymentReminderLog
/// </summary>
public class PaymentReminderLog
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int InvoiceId { get; set; }

    public int CustomerId { get; set; }

    public string RecipientEmail { get; set; } = null!;

    public string EscalationTier { get; set; } = null!;

    public bool IsSentSuccessfully { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsManualTrigger { get; set; }

    public DateTime SentAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // --- Open Tracking ---
    public string? TrackingToken { get; set; }
    public bool IsOpened { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public int OpenCount { get; set; }
    public DateTime? LastOpenedAtUtc { get; set; }

    // --- Test Send Flag ---
    public bool IsTestSend { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Invoice Invoice { get; set; } = null!;

    public Customer Customer { get; set; } = null!;
}
