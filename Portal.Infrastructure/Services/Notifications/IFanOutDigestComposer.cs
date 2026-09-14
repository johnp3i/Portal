using Portal.Infrastructure.Entities.Notification;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// A scheduled composer that fans OUT to multiple recipients — producing zero-or-more
/// <see cref="OutboxMessage"/> for a business in a single run (e.g. the Task &amp; Meeting Reminder,
/// which emails each assigned team member their own agenda). Distinct from
/// <see cref="IDigestComposer"/>, which produces a single message. A given AssistantKey is handled
/// by exactly one of the two interfaces.
///
/// Each returned message carries its own recipient-scoped <c>CycleKey</c>; the runner enqueues each
/// via <see cref="IDigestEnqueuer"/>, which de-dupes per (business, assistant, cycle key).
/// </summary>
public interface IFanOutDigestComposer
{
    /// <summary>The [notification].[AssistantType].[Key] this composer handles.</summary>
    string AssistantKey { get; }

    /// <summary>
    /// Builds one message per recipient who has something to report. Returns an empty list when
    /// there is nothing to send (the runner then enqueues nothing). Receives the business-local
    /// "now" so recipient-scoped daily cycle keys align to the business time zone.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ComposeManyAsync(
        int businessId,
        int assistantTypeId,
        BusinessAssistantSetting? setting,
        DateTime businessLocalNow,
        CancellationToken ct);
}
