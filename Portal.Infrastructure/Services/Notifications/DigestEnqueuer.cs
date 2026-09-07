using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Repositories.Notification;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Inserts a composed digest into the notification outbox with an insert-time cycle-dedup
/// re-check (mirrors <see cref="NotificationProducer.InsertAsync"/>, but keyed on the exact
/// <c>CycleKey</c>). Guards against concurrent scheduler passes enqueuing the same
/// (business, assistant, cycle) twice. A previously failed cycle can re-enqueue (the exists
/// check excludes Failed rows).
/// </summary>
public interface IDigestEnqueuer
{
    /// <summary>Returns true if the message was enqueued; false if a duplicate was detected.</summary>
    Task<bool> EnqueueAsync(OutboxMessage message);
}

public class DigestEnqueuer : IDigestEnqueuer
{
    private readonly NotificationOutboxRepository _outboxRepository;
    private readonly ILogger<DigestEnqueuer> _logger;

    public DigestEnqueuer(NotificationOutboxRepository outboxRepository, ILogger<DigestEnqueuer> logger)
    {
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public async Task<bool> EnqueueAsync(OutboxMessage message)
    {
        try
        {
            if (string.IsNullOrEmpty(message.CycleKey))
                throw new InvalidOperationException("Digest outbox message must have a CycleKey set.");

            // Final dedup check at insert time (closes the concurrent-pass race).
            var alreadyExists = await _outboxRepository.ExistsForCycleAsync(
                message.BusinessId, message.AssistantTypeId, message.CycleKey);
            if (alreadyExists)
            {
                _logger.LogInformation(
                    "Digest enqueue skipped — a non-failed message already exists for " +
                    "BusinessId={BusinessId}, AssistantTypeId={AssistantTypeId}, CycleKey={CycleKey}.",
                    message.BusinessId, message.AssistantTypeId, message.CycleKey);
                return false;
            }

            await _outboxRepository.InsertAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
