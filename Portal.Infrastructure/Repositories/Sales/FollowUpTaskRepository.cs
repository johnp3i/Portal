using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[FollowUpTask] entity CRUD operations.
/// </summary>
public class FollowUpTaskRepository : GenericStoredProcedureRepository<FollowUpTask>
{
    public FollowUpTaskRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(FollowUpTask entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[FollowUpTask]
                    ([BusinessId], [LeadRequestId], [ContactId], [TeamMemberId],
                     [Title], [TaskType], [DueAtUtc], [Notes],
                     [IsCompleted], [SnoozedCount], [CreatedByUserId], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @LeadRequestId, @ContactId, @TeamMemberId,
                     @Title, @TaskType, @DueAtUtc, @Notes,
                     0, 0, @CreatedByUserId, @CreatedAtUtc);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
                command.Parameters.Add(new SqlParameter("@LeadRequestId", entity.LeadRequestId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ContactId", entity.ContactId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@TeamMemberId", entity.TeamMemberId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Title", entity.Title));
                command.Parameters.Add(new SqlParameter("@TaskType", entity.TaskType));
                command.Parameters.Add(new SqlParameter("@DueAtUtc", entity.DueAtUtc));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedByUserId", entity.CreatedByUserId));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", DateTime.UtcNow));

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task CompleteAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[FollowUpTask]
                SET [IsCompleted] = 1,
                    [CompletedAtUtc] = @CompletedAtUtc
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId AND [IsCompleted] = 0";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@CompletedAtUtc", DateTime.UtcNow)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task SnoozeAsync(int id, int businessId, DateTime newDueAtUtc)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[FollowUpTask]
                SET [DueAtUtc] = @NewDueAtUtc,
                    [SnoozedCount] = [SnoozedCount] + 1
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId AND [IsCompleted] = 0";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@NewDueAtUtc", newDueAtUtc)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<FollowUpTask?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [TeamMemberId],
                       [Title], [TaskType], [DueAtUtc], [Notes],
                       [IsCompleted], [CompletedAtUtc], [SnoozedCount],
                       [CreatedByUserId], [CreatedAtUtc]
                FROM [sales].[FollowUpTask]
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets tasks due today, overdue, and optionally tomorrow for the Today's Actions panel.
    /// Returns non-completed tasks ordered by DueAtUtc ASC (max 10 nearest tasks).
    /// </summary>
    public async Task<List<FollowUpTask>> GetTodaysActionsAsync(int businessId, int? teamMemberId)
    {
        try
        {
            var query = @"
                SELECT TOP 10 [Id], [BusinessId], [LeadRequestId], [ContactId], [TeamMemberId],
                       [Title], [TaskType], [DueAtUtc], [Notes],
                       [IsCompleted], [CompletedAtUtc], [SnoozedCount],
                       [CreatedByUserId], [CreatedAtUtc]
                FROM [sales].[FollowUpTask]
                WHERE [BusinessId] = @BusinessId
                  AND [IsCompleted] = 0";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId)
            };

            if (teamMemberId.HasValue)
            {
                query += " AND [TeamMemberId] = @TeamMemberId";
                parameters.Add(new SqlParameter("@TeamMemberId", teamMemberId.Value));
            }

            query += " ORDER BY [DueAtUtc] ASC";

            var results = await ExecuteStoredProcedureUnfiltered(query, parameters.ToArray());
            return results.ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all tasks for a specific lead (pending first, then completed).
    /// </summary>
    public async Task<List<FollowUpTask>> GetByLeadRequestIdAsync(int leadRequestId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [TeamMemberId],
                       [Title], [TaskType], [DueAtUtc], [Notes],
                       [IsCompleted], [CompletedAtUtc], [SnoozedCount],
                       [CreatedByUserId], [CreatedAtUtc]
                FROM [sales].[FollowUpTask]
                WHERE [LeadRequestId] = @LeadRequestId AND [BusinessId] = @BusinessId
                ORDER BY [IsCompleted] ASC, [DueAtUtc] ASC";

            var results = await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@LeadRequestId", leadRequestId),
                new SqlParameter("@BusinessId", businessId));
            return results.ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the count of overdue tasks for a business (optionally filtered by team member).
    /// </summary>
    public async Task<int> GetOverdueCountAsync(int businessId, int? teamMemberId)
    {
        try
        {
            var today = DateTime.UtcNow.Date;

            var query = @"
                SELECT COUNT(*)
                FROM [sales].[FollowUpTask]
                WHERE [BusinessId] = @BusinessId
                  AND [IsCompleted] = 0
                  AND [DueAtUtc] < @Today";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Today", today)
            };

            if (teamMemberId.HasValue)
            {
                query += " AND [TeamMemberId] = @TeamMemberId";
                parameters.Add(new SqlParameter("@TeamMemberId", teamMemberId.Value));
            }

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                foreach (var param in parameters)
                    command.Parameters.Add(param);

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets paged tasks with optional filtering.
    /// </summary>
    public async Task<(List<FollowUpTask> Items, int TotalCount)> GetPagedAsync(
        int businessId, string? status, string? taskType, int? teamMemberId,
        DateTime? dateFrom, DateTime? dateTo, int page, int pageSize)
    {
        try
        {
            var baseWhere = "[BusinessId] = @BusinessId";
            var parameters = new List<SqlParameter> { new SqlParameter("@BusinessId", businessId) };

            if (!string.IsNullOrEmpty(status))
            {
                switch (status.ToLower())
                {
                    case "overdue":
                        baseWhere += " AND [IsCompleted] = 0 AND [DueAtUtc] < @Today";
                        parameters.Add(new SqlParameter("@Today", DateTime.UtcNow.Date));
                        break;
                    case "pending":
                        baseWhere += " AND [IsCompleted] = 0";
                        break;
                    case "completed":
                        baseWhere += " AND [IsCompleted] = 1";
                        break;
                }
            }

            if (!string.IsNullOrEmpty(taskType))
            {
                baseWhere += " AND [TaskType] = @TaskType";
                parameters.Add(new SqlParameter("@TaskType", taskType));
            }

            if (teamMemberId.HasValue)
            {
                baseWhere += " AND [TeamMemberId] = @TeamMemberId";
                parameters.Add(new SqlParameter("@TeamMemberId", teamMemberId.Value));
            }

            if (dateFrom.HasValue)
            {
                baseWhere += " AND [DueAtUtc] >= @DateFrom";
                parameters.Add(new SqlParameter("@DateFrom", dateFrom.Value));
            }

            if (dateTo.HasValue)
            {
                baseWhere += " AND [DueAtUtc] <= @DateTo";
                parameters.Add(new SqlParameter("@DateTo", dateTo.Value.Date.AddDays(1)));
            }

            // Count query
            var countQuery = $"SELECT COUNT(*) FROM [sales].[FollowUpTask] WHERE {baseWhere}";

            var connection = _context.Database.GetDbConnection();
            int totalCount;

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;
                    var transaction = _context.Database.CurrentTransaction;
                    if (transaction != null)
                        countCommand.Transaction = transaction.GetDbTransaction();

                    foreach (var p in parameters)
                        countCommand.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));

                    totalCount = (int)(await countCommand.ExecuteScalarAsync())!;
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            // Data query
            var offset = (page - 1) * pageSize;
            var dataQuery = $@"
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [TeamMemberId],
                       [Title], [TaskType], [DueAtUtc], [Notes],
                       [IsCompleted], [CompletedAtUtc], [SnoozedCount],
                       [CreatedByUserId], [CreatedAtUtc]
                FROM [sales].[FollowUpTask]
                WHERE {baseWhere}
                ORDER BY [IsCompleted] ASC, [DueAtUtc] ASC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            parameters.Add(new SqlParameter("@Offset", offset));
            parameters.Add(new SqlParameter("@PageSize", pageSize));

            var items = await ExecuteStoredProcedureUnfiltered(dataQuery, parameters.ToArray());
            return (items.ToList(), totalCount);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
