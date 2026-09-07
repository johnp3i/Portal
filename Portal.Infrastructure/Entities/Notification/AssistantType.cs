namespace Portal.Infrastructure.Entities.Notification;

/// <summary>
/// Static registry of Digital Assistants. Schema: [notification].AssistantType (seeded).
/// </summary>
public class AssistantType
{
    public int Id { get; set; }

    /// <summary>Stable key, e.g. "thank_you".</summary>
    public string Key { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    /// <summary>'Customer' | 'PortalUser'.</summary>
    public string RecipientKind { get; set; } = null!;

    public bool IsCustomerFacing { get; set; }
}
