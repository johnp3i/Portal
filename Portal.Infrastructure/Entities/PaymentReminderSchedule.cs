namespace Portal.Infrastructure.Entities;

/// <summary>
/// Per-tier reminder schedule configuration for a Business.
/// Each row represents one escalation tier (Friendly, Firm, or Formal).
/// Schema: [reminder].PaymentReminderSchedule
/// </summary>
public class PaymentReminderSchedule
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string EscalationTier { get; set; } = null!;

    public int DaysOffset { get; set; }

    public int MaxRemindersPerTier { get; set; }

    public int MinIntervalDays { get; set; }

    public int PartialPaymentSuppressionDays { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;
}
