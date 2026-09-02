using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[Meeting] entity CRUD operations.
/// </summary>
public class MeetingRepository : GenericStoredProcedureRepository<Meeting>
{
    public MeetingRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(Meeting entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[Meeting]
                    ([BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                     [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                     [Notes], [IsCancelled], [IsActive], [CreatedByUserId], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @LeadRequestId, @ContactId, @MeetingTypeId,
                     @Subject, @ScheduledAtUtc, @DurationMinutes, @Location,
                     @Notes, 0, 1, @CreatedByUserId, @CreatedAtUtc);
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
                command.Parameters.Add(new SqlParameter("@ContactId", entity.ContactId));
                command.Parameters.Add(new SqlParameter("@MeetingTypeId", entity.MeetingTypeId));
                command.Parameters.Add(new SqlParameter("@Subject", entity.Subject));
                command.Parameters.Add(new SqlParameter("@ScheduledAtUtc", entity.ScheduledAtUtc));
                command.Parameters.Add(new SqlParameter("@DurationMinutes", entity.DurationMinutes));
                command.Parameters.Add(new SqlParameter("@Location", entity.Location ?? (object)DBNull.Value));
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

    public async Task UpdateAsync(Meeting entity)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Meeting]
                SET [LeadRequestId] = @LeadRequestId,
                    [MeetingTypeId] = @MeetingTypeId,
                    [Subject] = @Subject,
                    [ScheduledAtUtc] = @ScheduledAtUtc,
                    [DurationMinutes] = @DurationMinutes,
                    [Location] = @Location,
                    [Notes] = @Notes,
                    [Outcome] = @Outcome,
                    [MeetingOutcomeClassificationId] = @MeetingOutcomeClassificationId
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@LeadRequestId", entity.LeadRequestId ?? (object)DBNull.Value),
                new SqlParameter("@MeetingTypeId", entity.MeetingTypeId),
                new SqlParameter("@Subject", entity.Subject),
                new SqlParameter("@ScheduledAtUtc", entity.ScheduledAtUtc),
                new SqlParameter("@DurationMinutes", entity.DurationMinutes),
                new SqlParameter("@Location", entity.Location ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@Outcome", entity.Outcome ?? (object)DBNull.Value),
                new SqlParameter("@MeetingOutcomeClassificationId", entity.MeetingOutcomeClassificationId ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task CancelAsync(int id, int businessId, string? description)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Meeting]
                SET [IsCancelled] = 1,
                    [CancellationTimestamp] = @CancellationTimestamp,
                    [CancellationDescription] = @CancellationDescription
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@CancellationTimestamp", DateTime.UtcNow),
                new SqlParameter("@CancellationDescription", description ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task ReactivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Meeting]
                SET [IsCancelled] = 0,
                    [CancellationTimestamp] = NULL,
                    [CancellationDescription] = NULL
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<Meeting?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc],
                       [MeetingOutcomeClassificationId]
                FROM [sales].[Meeting]
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<Meeting>> GetByLeadRequestIdAsync(int leadRequestId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc],
                       [MeetingOutcomeClassificationId]
                FROM [sales].[Meeting]
                WHERE [LeadRequestId] = @LeadRequestId
                  AND [BusinessId] = @BusinessId
                  AND [IsCancelled] = 0
                  AND [IsActive] = 1";

            var results = await ExecuteStoredProcedure(query,
                new SqlParameter("@LeadRequestId", leadRequestId),
                new SqlParameter("@BusinessId", businessId));
            return results.OrderByDescending(x => x.ScheduledAtUtc).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<Meeting?> GetUpcomingByLeadRequestIdAsync(int leadRequestId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT TOP 1
                       [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc],
                       [MeetingOutcomeClassificationId]
                FROM [sales].[Meeting]
                WHERE [LeadRequestId] = @LeadRequestId
                  AND [BusinessId] = @BusinessId
                  AND [IsCancelled] = 0
                  AND [IsActive] = 1
                  AND [ScheduledAtUtc] > GETUTCDATE()
                ORDER BY [ScheduledAtUtc] ASC";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@LeadRequestId", leadRequestId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<Meeting>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc],
                       [MeetingOutcomeClassificationId]
                FROM [sales].[Meeting]
                WHERE [BusinessId] = @BusinessId AND [IsActive] = 1";

            var results = await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
            return results.OrderByDescending(x => x.ScheduledAtUtc).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<Meeting>> GetUpcomingBriefAsync(int businessId, DateTime todayStart, DateTime endDate)
    {
        try
        {
            const string query = @"
                SELECT TOP 10
                       [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc],
                       [MeetingOutcomeClassificationId]
                FROM [sales].[Meeting]
                WHERE [sales].[Meeting].[BusinessId] = @BusinessId
                  AND [sales].[Meeting].[IsActive] = 1
                  AND [sales].[Meeting].[IsCancelled] = 0
                  AND [sales].[Meeting].[ScheduledAtUtc] >= @TodayStart
                  AND [sales].[Meeting].[ScheduledAtUtc] < @EndDate
                ORDER BY [sales].[Meeting].[ScheduledAtUtc] ASC";

            var results = await ExecuteStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@TodayStart", todayStart),
                new SqlParameter("@EndDate", endDate));
            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<Meeting>> GetDashboardMeetingsBriefAsync(int businessId, DateTime todayStart, DateTime dayAfterTomorrow)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc],
                       [MeetingOutcomeClassificationId]
                FROM [sales].[Meeting]
                WHERE [sales].[Meeting].[IsActive] = 1
                  AND [sales].[Meeting].[IsCancelled] = 0
                  AND [sales].[Meeting].[BusinessId] = @BusinessId
                  AND [sales].[Meeting].[ScheduledAtUtc] >= @TodayStart
                  AND [sales].[Meeting].[ScheduledAtUtc] < @DayAfterTomorrow
                ORDER BY [sales].[Meeting].[ScheduledAtUtc] ASC
                OFFSET 0 ROWS";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@TodayStart", todayStart),
                new SqlParameter("@DayAfterTomorrow", dayAfterTomorrow));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets paged meetings with optional filtering.
    /// </summary>
    public async Task<(List<Meeting> Items, int TotalCount)> GetPagedAsync(
        int businessId, string? status, int? meetingTypeId,
        DateTime? dateFrom, DateTime? dateTo, int? outcomeClassificationId, int page, int pageSize)
    {
        try
        {
            var baseWhere = "[BusinessId] = @BusinessId AND [IsActive] = 1";
            var parameters = new List<SqlParameter> { new SqlParameter("@BusinessId", businessId) };

            if (!string.IsNullOrEmpty(status))
            {
                switch (status.ToLower())
                {
                    case "upcoming":
                        baseWhere += " AND [ScheduledAtUtc] > @Now AND [IsCancelled] = 0";
                        parameters.Add(new SqlParameter("@Now", DateTime.UtcNow));
                        break;
                    case "completed":
                        baseWhere += " AND [ScheduledAtUtc] < @Now AND [IsCancelled] = 0";
                        parameters.Add(new SqlParameter("@Now", DateTime.UtcNow));
                        break;
                    case "cancelled":
                        baseWhere += " AND [IsCancelled] = 1";
                        break;
                }
            }

            if (meetingTypeId.HasValue)
            {
                baseWhere += " AND [MeetingTypeId] = @MeetingTypeId";
                parameters.Add(new SqlParameter("@MeetingTypeId", meetingTypeId.Value));
            }

            if (dateFrom.HasValue)
            {
                baseWhere += " AND [ScheduledAtUtc] >= @DateFrom";
                parameters.Add(new SqlParameter("@DateFrom", dateFrom.Value));
            }

            if (dateTo.HasValue)
            {
                baseWhere += " AND [ScheduledAtUtc] <= @DateTo";
                parameters.Add(new SqlParameter("@DateTo", dateTo.Value.Date.AddDays(1)));
            }

            if (outcomeClassificationId.HasValue)
            {
                baseWhere += " AND [MeetingOutcomeClassificationId] = @OutcomeClassificationId";
                parameters.Add(new SqlParameter("@OutcomeClassificationId", outcomeClassificationId.Value));
            }

            // Count query
            var countQuery = $"SELECT COUNT(*) FROM [sales].[Meeting] WHERE {baseWhere}";

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
                SELECT [Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId],
                       [Subject], [ScheduledAtUtc], [DurationMinutes], [Location],
                       [Notes], [Outcome], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc],
                       [MeetingOutcomeClassificationId]
                FROM [sales].[Meeting]
                WHERE {baseWhere}
                ORDER BY [ScheduledAtUtc] ASC
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

    /// <summary>
    /// Batch-fetches meeting subjects by IDs. Used for enriching task DTOs with meeting context.
    /// </summary>
    public async Task<Dictionary<int, string>> GetSubjectsByIdsAsync(IEnumerable<int> ids, int businessId)
    {
        var result = new Dictionary<int, string>();
        var idsList = ids.ToList();
        if (idsList.Count == 0) return result;

        try
        {
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var paramNames = new List<string>();
                var parameters = new List<SqlParameter> { new SqlParameter("@BusinessId", businessId) };

                for (int i = 0; i < idsList.Count; i++)
                {
                    var paramName = $"@Id{i}";
                    paramNames.Add(paramName);
                    parameters.Add(new SqlParameter(paramName, idsList[i]));
                }

                var inClause = string.Join(", ", paramNames);
                var query = $@"
                    SELECT [Id], [Subject]
                    FROM [sales].[Meeting]
                    WHERE [Id] IN ({inClause}) AND [BusinessId] = @BusinessId";

                using var command = connection.CreateCommand();
                command.CommandText = query;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                foreach (var p in parameters)
                    command.Parameters.Add(p);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result[reader.GetInt32(0)] = reader.GetString(1);
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
