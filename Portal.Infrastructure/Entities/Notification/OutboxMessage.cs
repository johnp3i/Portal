namespace Portal.Infrastructure.Entities.Notification;

/// <summary>
/// A durable, pending/sent/failed notification message.
/// Schema: [notification].OutboxMessage.
/// Written transactionally with the business event that triggered it; drained by the
/// in-process dispatcher. Also serves as the assistant activity log.
/// </summary>
public class OutboxMessage
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int AssistantTypeId { get; set; }

    public string RecipientEmail { get; set; } = null!;

    public string? RecipientName { get; set; }

    public string? ReplyToEmail { get; set; }

    public string Subject { get; set; } = null!;

    /// <summary>The fully-rendered HTML body, stored at write time (self-contained).</summary>
    public string BodyHtml { get; set; } = null!;

    /// <summary>FK to [notification].OutboxMessageStatusType (1=Pending, 2=Sent, 3=Failed).</summary>
    public int OutboxMessageStatusTypeId { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetries { get; set; }

    /// <summary>The dispatcher picks up the message when this time has passed.</summary>
    public DateTime ScheduledForUtc { get; set; }

    public string? LastError { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? FailedAtUtc { get; set; }

    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    /// <summary>
    /// Exact per-cycle idempotency key for scheduled/cycle-based producers
    /// (e.g. "weekly_financial_snapshot:2026-W35"). NULL for event-driven producers
    /// (e.g. Thank-You), which dedup via RelatedEntityType/RelatedEntityId instead.
    /// </summary>
    public string? CycleKey { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public Business Business { get; set; } = null!;

    public AssistantType AssistantType { get; set; } = null!;

    public OutboxMessageStatusType OutboxMessageStatusType { get; set; } = null!;
}
