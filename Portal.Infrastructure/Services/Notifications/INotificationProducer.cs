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
    /// Resolves + gates an owner-facing "New Payment Received" alert for a recorded payment.
    /// Resolves the business owner's email (returns null if none), applies per-business gating,
    /// and renders the body. Returns a ready-to-insert <see cref="OutboxMessage"/> or null.
    /// No entity-dedup: the payment paths are either deliberate user actions or already
    /// idempotent upstream (the Stripe webhook short-circuits before inserting a duplicate
    /// payment), so a stamped dedup key would be redundant. Does NOT write to the database.
    /// <paramref name="invoiceNumber"/> is null for global/unallocated payments.
    /// </summary>
    Task<OutboxMessage?> PrepareNewPaymentAsync(
        int businessId,
        string? invoiceNumber,
        string? customerName,
        decimal amount);

    /// <summary>
    /// Inserts a prepared outbox message. Intended to be called INSIDE the transaction that
    /// records the triggering business event, so the two commit atomically. Honours any ambient
    /// transaction on the shared DbContext.
    /// </summary>
    Task InsertAsync(OutboxMessage message);
}
