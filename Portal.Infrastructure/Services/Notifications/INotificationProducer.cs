using Portal.Infrastructure.Entities.Notification;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Builds notification outbox messages when business events occur, applying per-business
/// assistant gating (enabled? opted out? deduped?) and working-hours scheduling. Producers
/// live in Infrastructure and only produce/insert outbox rows; the Web-side dispatcher sends.
///
/// The API is split so callers can do all the (read-only) resolution and gating work OUTSIDE
/// a database transaction, then insert the prepared row INSIDE the transaction that records the
/// triggering business event — keeping that hot-path transaction as short as possible.
/// </summary>
public interface INotificationProducer
{
    /// <summary>
    /// Resolves + gates a Thank-You notification for a recorded payment. Performs all reads
    /// (assistant lookup, per-business setting, opt-out, dedup, currency/name/timezone) and
    /// renders the body. Returns a ready-to-insert <see cref="OutboxMessage"/>, or null if no
    /// notification should be sent (assistant disabled, opted out, no email, or already queued).
    /// Does NOT write to the database — safe to call before opening a transaction.
    /// </summary>
    Task<OutboxMessage?> PrepareThankYouAsync(
        int businessId,
        int invoiceId,
        string customerName,
        string customerEmail,
        decimal amount,
        string invoiceNumber,
        string? replyToEmail);

    /// <summary>
    /// Inserts a prepared outbox message. Intended to be called INSIDE the transaction that
    /// records the triggering business event, so the two commit atomically. Honours any ambient
    /// transaction on the shared DbContext.
    /// </summary>
    Task InsertAsync(OutboxMessage message);
}
