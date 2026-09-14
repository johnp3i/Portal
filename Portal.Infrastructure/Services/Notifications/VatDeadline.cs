using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Single definition of a VAT period's DERIVED filing deadline. The schema stores no deadline
/// column, so it is always computed as <c>PeriodEndDate + NotificationOptions.VatFilingOffsetDays</c>
/// (jurisdiction-tuned; Cyprus ≈ 40 days). Shared by <see cref="AttentionItemBuilder"/> and the VAT
/// Period Due Reminder composer so the two can never drift.
/// </summary>
public static class VatDeadline
{
    /// <summary>The derived filing deadline for a VAT period.</summary>
    public static DateOnly For(VatSubmissionPeriod period, NotificationOptions options)
        => period.PeriodEndDate.AddDays(options.VatFilingOffsetDays);
}
