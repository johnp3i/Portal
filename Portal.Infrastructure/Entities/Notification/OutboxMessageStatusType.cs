namespace Portal.Infrastructure.Entities.Notification;

/// <summary>
/// Reference table for outbox message delivery status.
/// Schema: [notification].OutboxMessageStatusType (seeded: 1=Pending, 2=Sent, 3=Failed).
/// </summary>
public class OutboxMessageStatusType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
