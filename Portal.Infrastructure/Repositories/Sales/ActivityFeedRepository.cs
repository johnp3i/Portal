using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    /// <summary>
    /// Returns all activity feed entries for a lead, ordered by CreatedAtUtc descending.
    /// Used by the timeline service to display all events chronologically.
    /// </summary>
    public async Task<List<ActivityFeedEntry>> GetByLeadRequestIdAsync(int leadRequestId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [Action], [Description],
                       [PerformedByUserId], [PerformedByTeamMemberId], [Metadata], [CreatedAtUtc]
                FROM [sales].[ActivityFeed]
                WHERE [LeadRequestId] = @LeadRequestId
                ORDER BY [CreatedAtUtc] DESC";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@LeadRequestId", leadRequestId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns all activity feed entries where Action = 'stage_changed' and CreatedAtUtc
    /// is within [startDate, endDate) for leads belonging to the given businessId.
    /// Used by InsightsService for conversion rate computation.
    /// </summary>
    public async Task<List<ActivityFeedEntry>> GetStageChangesInRangeAsync(DateTime startDate, DateTime endDate, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [sales].[ActivityFeed].[Id],
                       [sales].[ActivityFeed].[BusinessId],
                       [sales].[ActivityFeed].[LeadRequestId],
                       [sales].[ActivityFeed].[Action],
                       [sales].[ActivityFeed].[Description],
                       [sales].[ActivityFeed].[PerformedByUserId],
                       [sales].[ActivityFeed].[PerformedByTeamMemberId],
                       [sales].[ActivityFeed].[Metadata],
                       [sales].[ActivityFeed].[CreatedAtUtc]
                FROM [sales].[ActivityFeed]
                WHERE [sales].[ActivityFeed].[Action] = @Action
                  AND [sales].[ActivityFeed].[CreatedAtUtc] >= @StartDate
                  AND [sales].[ActivityFeed].[CreatedAtUtc] < @EndDate
                  AND [sales].[ActivityFeed].[BusinessId] = @BusinessId";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@Action", "stage_changed"),
                new SqlParameter("@StartDate", startDate),
                new SqlParameter("@EndDate", endDate),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns paged activity entries filtered by optional action type and date range.
    /// </summary>
    public async Task<List<ActivityFeedEntry>> GetPagedByBusinessIdAsync(int businessId, string? actionType, DateTime? dateFrom, DateTime? dateTo, int page, int pageSize)
    {
        try
        {
            var offset = (page - 1) * pageSize;
            const string query = @"
                SELECT [sales].[ActivityFeed].[Id],
                       [sales].[ActivityFeed].[BusinessId],
                       [sales].[ActivityFeed].[LeadRequestId],
                       [sales].[ActivityFeed].[Action],
                       [sales].[ActivityFeed].[Description],
                       [sales].[ActivityFeed].[PerformedByUserId],
                       [sales].[ActivityFeed].[PerformedByTeamMemberId],
                       [sales].[ActivityFeed].[Metadata],
                       [sales].[ActivityFeed].[CreatedAtUtc]
                FROM [sales].[ActivityFeed]
                WHERE [sales].[ActivityFeed].[BusinessId] = @BusinessId
                  AND (@ActionType IS NULL OR [sales].[ActivityFeed].[Action] = @ActionType)
                  AND (@DateFrom IS NULL OR [sales].[ActivityFeed].[CreatedAtUtc] >= @DateFrom)
                  AND (@DateTo IS NULL OR [sales].[ActivityFeed].[CreatedAtUtc] < @DateTo)
                ORDER BY [sales].[ActivityFeed].[CreatedAtUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@ActionType", actionType ?? (object)DBNull.Value),
                new SqlParameter("@DateFrom", dateFrom ?? (object)DBNull.Value),
                new SqlParameter("@DateTo", dateTo ?? (object)DBNull.Value),
                new SqlParameter("@Offset", offset),
                new SqlParameter("@PageSize", pageSize));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns total count of activity entries matching the filter criteria.
    /// </summary>
    public async Task<int> GetCountByBusinessIdAsync(int businessId, string? actionType, DateTime? dateFrom, DateTime? dateTo)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [sales].[ActivityFeed]
                WHERE [sales].[ActivityFeed].[BusinessId] = @BusinessId
                  AND (@ActionType IS NULL OR [sales].[ActivityFeed].[Action] = @ActionType)
                  AND (@DateFrom IS NULL OR [sales].[ActivityFeed].[CreatedAtUtc] >= @DateFrom)
                  AND (@DateTo IS NULL OR [sales].[ActivityFeed].[CreatedAtUtc] < @DateTo)";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@ActionType", actionType ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@DateFrom", dateFrom ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@DateTo", dateTo ?? (object)DBNull.Value));

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
            }
            finally
            {
                if (connection.State == System.Data.ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns the most recent N entries for the business, ordered by CreatedAtUtc DESC.
    /// </summary>
    public async Task<List<ActivityFeedEntry>> GetRecentByBusinessIdAsync(int businessId, int count)
    {
        try
        {
            const string query = @"
                SELECT TOP(@Count)
                       [sales].[ActivityFeed].[Id],
                       [sales].[ActivityFeed].[BusinessId],
                       [sales].[ActivityFeed].[LeadRequestId],
                       [sales].[ActivityFeed].[Action],
                       [sales].[ActivityFeed].[Description],
                       [sales].[ActivityFeed].[PerformedByUserId],
                       [sales].[ActivityFeed].[PerformedByTeamMemberId],
                       [sales].[ActivityFeed].[Metadata],
                       [sales].[ActivityFeed].[CreatedAtUtc]
                FROM [sales].[ActivityFeed]
                WHERE [sales].[ActivityFeed].[BusinessId] = @BusinessId
                ORDER BY [sales].[ActivityFeed].[CreatedAtUtc] DESC";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@Count", count),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
