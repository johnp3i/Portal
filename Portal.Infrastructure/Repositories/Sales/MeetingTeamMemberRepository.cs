using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Raw-SQL repository for [sales].[MeetingTeamMember] — the meeting attendee mapping.
/// Writes enlist in the shared PortalDbContext's ambient transaction when one is open.
/// </summary>
public class MeetingTeamMemberRepository : GenericStoredProcedureRepository<MeetingTeamMember>
{
    public MeetingTeamMemberRepository(DbContext context) : base(context) { }

    /// <summary>Attendee team-member ids for a single meeting.</summary>
    public async Task<List<int>> GetAttendeeIdsByMeetingIdAsync(int meetingId)
    {
        try
        {
            return await _context.Set<MeetingTeamMember>()
                .AsNoTracking()
                .Where(mtm => mtm.MeetingId == meetingId)
                .Select(mtm => mtm.TeamMemberId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Attendee rows for a set of meetings (meetingId -> teamMemberIds), for bulk scans.</summary>
    public async Task<Dictionary<int, List<int>>> GetAttendeeIdsByMeetingIdsAsync(IEnumerable<int> meetingIds)
    {
        try
        {
            var ids = meetingIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, List<int>>();

            var rows = await _context.Set<MeetingTeamMember>()
                .AsNoTracking()
                .Where(mtm => ids.Contains(mtm.MeetingId))
                .Select(mtm => new { mtm.MeetingId, mtm.TeamMemberId })
                .ToListAsync();

            return rows
                .GroupBy(r => r.MeetingId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.TeamMemberId).ToList());
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Replaces the attendee set for a meeting: deletes existing rows and inserts the given
    /// distinct member ids. Enlists in the ambient transaction when the caller opened one.
    /// </summary>
    public async Task ReplaceAttendeesAsync(int meetingId, IEnumerable<int> teamMemberIds)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM [sales].[MeetingTeamMember] WHERE [MeetingId] = @MeetingId",
                new SqlParameter("@MeetingId", meetingId));

            foreach (var teamMemberId in teamMemberIds.Distinct())
            {
                await _context.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO [sales].[MeetingTeamMember] ([MeetingId], [TeamMemberId], [CreatedAtUtc])
                      VALUES (@MeetingId, @TeamMemberId, GETUTCDATE())",
                    new SqlParameter("@MeetingId", meetingId),
                    new SqlParameter("@TeamMemberId", teamMemberId));
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
