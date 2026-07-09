namespace Portal.Infrastructure.Models;

/// <summary>
/// Weekly activity summary statistics for the Activity Log quick stats row.
/// </summary>
public class ActivityStatsDto
{
    public int ChangesThisWeek { get; set; }
    public int ActiveTeamMembers { get; set; }
    public string MostActiveArea { get; set; } = "None";
    public DateTime? LastActivityUtc { get; set; }
}
