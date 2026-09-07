using Portal.Infrastructure.Entities.Notification;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Composes one owner-facing digest into a ready-to-enqueue <see cref="OutboxMessage"/> for a
/// given business and cycle. Implementations gather data from existing (tenant-less) query
/// services, resolve the recipient, and render the HTML body via <see cref="DigestEmailBuilder"/>.
/// Returns null when the digest cannot be sent (e.g. no resolvable recipient) — the runner then
/// skips and logs, per Requirement 7.3.
/// </summary>
public interface IDigestComposer
{
    /// <summary>The [notification].[AssistantType].[Key] this composer handles.</summary>
    string AssistantKey { get; }

    Task<OutboxMessage?> ComposeAsync(
        int businessId,
        int assistantTypeId,
        BusinessAssistantSetting? setting,
        string cycleKey,
        CancellationToken ct);
}
