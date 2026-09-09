namespace Portal.Web.Models.Assistants;

public class AssistantsIndexViewModel
{
    public List<AssistantCardViewModel> Assistants { get; set; } = new();
}

public class AssistantCardViewModel
{
    public int AssistantTypeId { get; set; }
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool IsCustomerFacing { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsBrandingFooterEnabled { get; set; }
    public string? WorkingHoursStart { get; set; }
    public string? WorkingHoursEnd { get; set; }

    // --- Scheduled digest (owner-facing) fields ---
    /// <summary>True for any scheduled owner-facing assistant (weekly digest OR daily brief).</summary>
    public bool IsScheduledDigest { get; set; }
    /// <summary>True for the daily brief: send-time only card (no day-of-week, no figures).</summary>
    public bool IsDailyBrief { get; set; }
    /// <summary>True for an event alert (e.g. New Payment): recipient-only card, no schedule.</summary>
    public bool IsEventAlert { get; set; }
    /// <summary>0=Sunday .. 6=Saturday. Defaults to Monday (1) when unset.</summary>
    public byte SendDayOfWeek { get; set; } = 1;
    /// <summary>Send time (business-local) as "HH:mm". Defaults to 08:00 when unset.</summary>
    public string SendTimeLocal { get; set; } = "08:00";
    public string? RecipientOverride { get; set; }
    public bool IsRecipientOwnerIncluded { get; set; } = true;
    /// <summary>Selected snapshot figure keys (for the Financial Snapshot only).</summary>
    public List<string> IncludedFigures { get; set; } = new();
    /// <summary>True only for the Financial Snapshot (shows the figures checklist).</summary>
    public bool HasFigureSelection { get; set; }
}

public class ToggleAssistantRequest
{
    public string AssistantKey { get; set; } = null!;
    public bool Enabled { get; set; }
}

public class SaveAssistantSettingsRequest
{
    public string AssistantKey { get; set; } = null!;

    // Customer-facing assistants (e.g. Thank-You)
    public bool IsBrandingFooterEnabled { get; set; }
    public string? WorkingHoursStart { get; set; }
    public string? WorkingHoursEnd { get; set; }

    // Scheduled owner-facing digests
    public byte? SendDayOfWeek { get; set; }
    public string? SendTimeLocal { get; set; }
    public string? RecipientOverride { get; set; }
    public bool IsRecipientOwnerIncluded { get; set; } = true;
    /// <summary>CSV of selected snapshot figure keys (Financial Snapshot only).</summary>
    public string? IncludedFiguresCsv { get; set; }
}

public class AdminNotificationRecipientsViewModel
{
    /// <summary>Raw recipient list (semicolon/newline-delimited) as stored in PlatformConfig.</summary>
    public string Recipients { get; set; } = string.Empty;
}

public class SaveAdminRecipientsRequest
{
    public string? Recipients { get; set; }
}
