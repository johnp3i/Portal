namespace Portal.Infrastructure.Constants;

/// <summary>
/// Stable [notification].[AssistantType].[Key] values for the scheduled owner-facing
/// digest assistants (Group 3). Resolved by Key at runtime — mirrors
/// NotificationProducer.ThankYouKey — so code never hard-codes the seeded Id.
/// </summary>
public static class DigestAssistantKeys
{
    /// <summary>Weekly Outstanding Balance Digest (receivables + upcoming payables). Seed Id 2.</summary>
    public const string WeeklyOutstandingDigest = "weekly_outstanding_digest";

    /// <summary>Weekly Financial Snapshot (period figures + to-date outstanding). Seed Id 3.</summary>
    public const string WeeklyFinancialSnapshot = "weekly_financial_snapshot";

    /// <summary>Daily Brief — a daily-cadence owner attention summary (Phase 4a). Seed Id 5.</summary>
    public const string DailyBrief = "daily_brief";

    /// <summary>VAT Period Due Reminder — owner reminder before a VAT filing deadline (Group 4). Seed Id 6.</summary>
    public const string VatPeriodDueReminder = "vat_period_due_reminder";

    /// <summary>Task &amp; Meeting Reminder — per-team-member daily agenda of upcoming/overdue tasks + meetings (Group 4). Seed Id 7.</summary>
    public const string TaskMeetingReminder = "task_meeting_reminder";

    /// <summary>The related-entity type / cycle-key namespace marker for digest outbox rows.</summary>
    public const string RelatedEntityDigest = "Digest";
}
