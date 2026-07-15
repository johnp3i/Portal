namespace Portal.Infrastructure.Models;

/// <summary>
/// View model for the dashboard briefing card — narrative summary of business state.
/// </summary>
public class BriefingViewModel
{
    public string Greeting { get; set; } = null!;
    public string Subtitle { get; set; } = null!;
    public BriefingState State { get; set; }
    public List<BriefingInsight> Insights { get; set; } = new();
    public bool HasInsights => Insights.Count > 0;
    public string DateDisplay { get; set; } = null!;
}

public class BriefingInsight
{
    public int Priority { get; set; }
    public string Html { get; set; } = null!;
    public BriefingSeverity Severity { get; set; }
}

public enum BriefingState
{
    Normal,
    AllClear,
    Critical,
    NewBusiness
}

public enum BriefingSeverity
{
    Urgent,   // Red dot
    Action,   // Amber dot
    Positive  // Green dot
}
