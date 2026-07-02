using Portal.Infrastructure.Models.PaymentReminders;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Core payment reminder evaluation, sending, and querying logic.
/// Handles both automated daily evaluation and manual reminder dispatch.
/// All operations enforce tenant isolation via BusinessId.
/// </summary>
public interface IPaymentReminderService
{
    /// <summary>
    /// Evaluates all invoices for a business on a given date and sends applicable reminders.
    /// Applies all exclusion rules (opt-out, disputed, partial payment, max reminders, min interval)
    /// and enforces idempotency for the same evaluation date.
    /// </summary>
    Task<ReminderEvaluationResult> EvaluateAndSendAsync(int businessId, DateOnly evaluationDate);

    /// <summary>
    /// Sends a manual reminder for a specific invoice at the specified escalation tier.
    /// Validates invoice eligibility, customer email presence, opt-out status, and disputed flag.
    /// </summary>
    Task<ManualReminderResult> SendManualReminderAsync(int businessId, int invoiceId, string escalationTier);

    /// <summary>
    /// Gets the reminder history for an invoice, ordered by date descending.
    /// </summary>
    Task<List<PaymentReminderLogDto>> GetHistoryByInvoiceAsync(int businessId, int invoiceId);

    /// <summary>
    /// Gets dashboard widget data including reminders sent this week
    /// and payments received within 7 days of a reminder.
    /// </summary>
    Task<ReminderDashboardWidgetDto> GetDashboardWidgetDataAsync(int businessId);

    /// <summary>
    /// Gets all business IDs that have the payment_reminder_auto module permission,
    /// used by the background job to determine which businesses to evaluate.
    /// </summary>
    Task<List<int>> GetEligibleBusinessIdsAsync();

    /// <summary>
    /// Records an email open event for a tracking token.
    /// First open: sets IsOpened=true, OpenedAtUtc, OpenCount=1.
    /// Subsequent: increments OpenCount, updates LastOpenedAtUtc.
    /// </summary>
    Task RecordOpenEventAsync(string trackingToken);

    /// <summary>
    /// Sends a test reminder to an alternate email address.
    /// Creates a log entry with IsTestSend = true. Excluded from caps/metrics.
    /// </summary>
    Task<TestReminderResult> SendTestReminderAsync(int businessId, int invoiceId, string escalationTier, string testRecipientEmail);

    /// <summary>
    /// Projects upcoming reminders for the next N days using the same evaluation
    /// logic as EvaluateAndSendAsync but in dry-run mode (no sends, no log creation).
    /// </summary>
    Task<List<UpcomingReminderDto>> GetUpcomingRemindersAsync(int businessId, int daysAhead = 14, string? tierFilter = null);
}
