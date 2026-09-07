namespace Portal.Infrastructure.Entities.Notification;

/// <summary>
/// A per-service, per-recipient opt-out. Schema: [notification].AssistantOptOut.
/// A recipient can opt out of one assistant independently of others.
/// </summary>
public class AssistantOptOut
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int AssistantTypeId { get; set; }

    public int? CustomerId { get; set; }

    public string RecipientEmail { get; set; } = null!;

    public DateTime OptedOutAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public Business Business { get; set; } = null!;

    public AssistantType AssistantType { get; set; } = null!;
}
