namespace Portal.Infrastructure.Entities.Notification;

/// <summary>
/// Per-business configuration for a single assistant.
/// Schema: [notification].BusinessAssistantSetting. One row per (business, assistant).
/// Absence of a row means "use the assistant's defaults" (enabled, footer on, no hours).
/// </summary>
public class BusinessAssistantSetting
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int AssistantTypeId { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsBrandingFooterEnabled { get; set; }

    /// <summary>Start of the send window (business local time). NULL = no restriction.</summary>
    public TimeOnly? WorkingHoursStart { get; set; }

    /// <summary>End of the send window (business local time). NULL = no restriction.</summary>
    public TimeOnly? WorkingHoursEnd { get; set; }

    // --- Scheduled-digest settings (Group 3). NULL/default => apply read-time defaults.
    //     Used by scheduled owner-facing assistants; ignored by customer-facing ones. ---

    /// <summary>Send day for scheduled digests: 0=Sunday .. 6=Saturday. NULL => Monday.</summary>
    public byte? SendDayOfWeek { get; set; }

    /// <summary>Send time for scheduled digests (business local time). NULL => 08:00.</summary>
    public TimeOnly? SendTimeLocal { get; set; }

    /// <summary>Delimited override recipient email list. NULL => send to the business owner.</summary>
    public string? RecipientOverride { get; set; }

    /// <summary>When an override recipient is set, also CC the business owner?</summary>
    public bool IsRecipientOwnerIncluded { get; set; } = true;

    /// <summary>CSV of snapshot figure keys to include. NULL => the default figure set.</summary>
    public string? IncludedFiguresCsv { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation
    public Business Business { get; set; } = null!;

    public AssistantType AssistantType { get; set; } = null!;
}
