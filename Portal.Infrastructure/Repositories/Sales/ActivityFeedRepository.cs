using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[ActivityFeed] — immutable event log per lead.
/// </summary>
public class ActivityFeedRepository : GenericStoredProcedureRepository<ActivityFeedEntry>
{
    public ActivityFeedRepository(DbContext context) : base(context) { }

    public async Task InsertAsync(ActivityFeedEntry entry)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[ActivityFeed]
                    ([BusinessId], [LeadRequestId], [Action], [Description],
                     [PerformedByUserId], [PerformedByTeamMemberId], [Metadata])
                VALUES
                    (@BusinessId, @LeadRequestId, @Action, @Description,
                     @PerformedByUserId, @PerformedByTeamMemberId, @Metadata)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entry.BusinessId),
                new SqlParameter("@LeadRequestId", entry.LeadRequestId),
                new SqlParameter("@Action", entry.Action),
                new SqlParameter("@Description", entry.Description),
                new SqlParameter("@PerformedByUserId", entry.PerformedByUserId ?? (object)DBNull.Value),
                new SqlParameter("@PerformedByTeamMemberId", entry.PerformedByTeamMemberId ?? (object)DBNull.Value),
                new SqlParameter("@Metadata", entry.Metadata ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<ActivityFeedEntry>> GetByLeadRequestIdAsync(int leadRequestId, int businessId, int page = 1, int pageSize = 20)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [Action], [Description],
                       [PerformedByUserId], [PerformedByTeamMemberId], [Metadata], [CreatedAtUtc]
                FROM [sales].[ActivityFeed]
                WHERE [LeadRequestId] = @LeadRequestId AND [BusinessId] = @BusinessId
                ORDER BY [CreatedAtUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@LeadRequestId", leadRequestId),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Offset", (page - 1) * pageSize),
                new SqlParameter("@PageSize", pageSize));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<ActivityFeedEntry>> GetAllByBusinessIdAsync(int businessId, int page = 1, int pageSize = 15)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [Action], [Description],
                       [PerformedByUserId], [PerformedByTeamMemberId], [Metadata], [CreatedAtUtc]
                FROM [sales].[ActivityFeed]
                WHERE [BusinessId] = @BusinessId
                ORDER BY [CreatedAtUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Offset", (page - 1) * pageSize),
                new SqlParameter("@PageSize", pageSize));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
