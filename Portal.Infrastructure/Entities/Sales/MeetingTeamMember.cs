namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Maps a meeting to a team member assigned to attend it (many-to-many). Drives per-attendee
/// reminders in the Task & Meeting Reminder assistant.
/// Schema: [sales].[MeetingTeamMember]
/// </summary>
public class MeetingTeamMember
{
    public int Id { get; set; }

    public int MeetingId { get; set; }

    public int TeamMemberId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Meeting Meeting { get; set; } = null!;

    public TeamMember TeamMember { get; set; } = null!;
}
