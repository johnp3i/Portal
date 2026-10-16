using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[ProspectActivity] — a prospect's own activity timeline.
/// Recording an activity never creates a Lead or Sales Contact; entries survive conversion.
/// </summary>
public class ProspectActivityRepository : GenericStoredProcedureRepository<ProspectActivity>
{
    // Full entity column list — FromSqlRaw materializes every mapped property.
    private const string Columns =
        "[Id], [BusinessId], [ProspectId], [ActivityType], [OccurredAtUtc], [PerformedByUserId], " +
        "[Sentiment], [IsFollowUp], [Outcome], [Notes], [NextAction], [NextActionDate], " +
        "[NextActionTime], [NextActionContactName], [NextActionPhone], [CreatedAtUtc]";

    public ProspectActivityRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(ProspectActivity entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[ProspectActivity]
                    ([BusinessId], [ProspectId], [ActivityType], [OccurredAtUtc], [PerformedByUserId],
                     [Sentiment], [IsFollowUp], [Outcome], [Notes], [NextAction], [NextActionDate],
                     [NextActionTime], [NextActionContactName], [NextActionPhone], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @ProspectId, @ActivityType, @OccurredAtUtc, @PerformedByUserId,
                     @Sentiment, @IsFollowUp, @Outcome, @Notes, @NextAction, @NextActionDate,
                     @NextActionTime, @NextActionContactName, @NextActionPhone, @CreatedAtUtc);
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
                command.Parameters.Add(new SqlParameter("@ProspectId", entity.ProspectId));
                command.Parameters.Add(new SqlParameter("@ActivityType", entity.ActivityType));
                command.Parameters.Add(new SqlParameter("@OccurredAtUtc", entity.OccurredAtUtc == default ? DateTime.UtcNow : entity.OccurredAtUtc));
                command.Parameters.Add(new SqlParameter("@PerformedByUserId", entity.PerformedByUserId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Sentiment", entity.Sentiment.HasValue ? entity.Sentiment.Value : (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsFollowUp", entity.IsFollowUp));
                command.Parameters.Add(new SqlParameter("@Outcome", entity.Outcome ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@NextAction", entity.NextAction ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@NextActionDate", entity.NextActionDate.HasValue ? entity.NextActionDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@NextActionTime", SqlDbType.Time) { Value = entity.NextActionTime.HasValue ? entity.NextActionTime.Value.ToTimeSpan() : (object)DBNull.Value });
                command.Parameters.Add(new SqlParameter("@NextActionContactName", entity.NextActionContactName ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@NextActionPhone", entity.NextActionPhone ?? (object)DBNull.Value));
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

    /// <summary>Full timeline for a prospect, most recent first.</summary>
    public async Task<List<ProspectActivity>> GetByProspectAsync(int prospectId, int businessId)
    {
        try
        {
            // No ORDER BY in the raw SQL: the global query filter makes EF wrap this as a
            // subquery, and SQL Server rejects ORDER BY inside a derived table without TOP/OFFSET.
            // Order in LINQ so it lands on the outer query.
            var query = $@"
                SELECT {Columns}
                FROM [sales].[ProspectActivity]
                WHERE [ProspectId] = @ProspectId AND [BusinessId] = @BusinessId";

            var results = await _context.Set<ProspectActivity>()
                .FromSqlRaw(query,
                    new SqlParameter("@ProspectId", prospectId),
                    new SqlParameter("@BusinessId", businessId))
                .OrderByDescending(a => a.OccurredAtUtc)
                .ThenByDescending(a => a.Id)
                .ToListAsync();
            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Single activity by id, scoped to the tenant.</summary>
    public async Task<ProspectActivity?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            var query = $@"
                SELECT {Columns}
                FROM [sales].[ProspectActivity]
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

    /// <summary>Updates an activity's editable fields (type, when, outcome, notes, next action).</summary>
    public async Task UpdateActivityAsync(ProspectActivity entity)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[ProspectActivity]
                SET [ActivityType] = @ActivityType,
                    [OccurredAtUtc] = @OccurredAtUtc,
                    [Sentiment] = @Sentiment,
                    [IsFollowUp] = @IsFollowUp,
                    [Outcome] = @Outcome,
                    [Notes] = @Notes,
                    [NextAction] = @NextAction,
                    [NextActionDate] = @NextActionDate,
                    [NextActionTime] = @NextActionTime,
                    [NextActionContactName] = @NextActionContactName,
                    [NextActionPhone] = @NextActionPhone
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@ActivityType", entity.ActivityType),
                new SqlParameter("@OccurredAtUtc", entity.OccurredAtUtc),
                new SqlParameter("@Sentiment", entity.Sentiment.HasValue ? entity.Sentiment.Value : (object)DBNull.Value),
                new SqlParameter("@IsFollowUp", entity.IsFollowUp),
                new SqlParameter("@Outcome", entity.Outcome ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@NextAction", entity.NextAction ?? (object)DBNull.Value),
                new SqlParameter("@NextActionDate", entity.NextActionDate.HasValue ? entity.NextActionDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value),
                new SqlParameter("@NextActionTime", SqlDbType.Time) { Value = entity.NextActionTime.HasValue ? entity.NextActionTime.Value.ToTimeSpan() : (object)DBNull.Value },
                new SqlParameter("@NextActionContactName", entity.NextActionContactName ?? (object)DBNull.Value),
                new SqlParameter("@NextActionPhone", entity.NextActionPhone ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Batched "last activity per prospect" for a set of prospect ids. Returns a dictionary
    /// keyed by ProspectId holding each prospect's most recent activity. Used to fill the
    /// "Last Activity" column of the prospect working list without an N+1 query.
    /// </summary>
    public async Task<Dictionary<int, ProspectActivity>> GetLastActivityByProspectIdsAsync(IEnumerable<int> prospectIds, int businessId)
    {
        try
        {
            var idList = prospectIds.Distinct().ToList();
            if (idList.Count == 0)
                return new Dictionary<int, ProspectActivity>();

            var paramNames = idList.Select((_, i) => $"@Id{i}").ToList();
            var inClause = string.Join(", ", paramNames);

            // ROW_NUMBER partitioned by ProspectId, newest first; take row 1 per prospect.
            var query = $@"
                SELECT {Columns}
                FROM (
                    SELECT {Columns},
                           ROW_NUMBER() OVER (PARTITION BY [ProspectId] ORDER BY [OccurredAtUtc] DESC, [Id] DESC) AS [RowNum]
                    FROM [sales].[ProspectActivity]
                    WHERE [BusinessId] = @BusinessId AND [ProspectId] IN ({inClause})
                ) AS Ranked
                WHERE [RowNum] = 1";

            var result = new Dictionary<int, ProspectActivity>();
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

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                for (int i = 0; i < idList.Count; i++)
                    command.Parameters.Add(new SqlParameter($"@Id{i}", idList[i]));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var activity = ReadActivity(reader);
                    result[activity.ProspectId] = activity;
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

    private static ProspectActivity ReadActivity(System.Data.Common.DbDataReader r)
    {
        string? S(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : r.GetString(r.GetOrdinal(c));
        DateOnly? D(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal(c)));
        byte? B(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : r.GetByte(r.GetOrdinal(c));
        // TIME columns surface as TimeSpan via the ADO reader.
        TimeOnly? T(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : TimeOnly.FromTimeSpan((TimeSpan)r.GetValue(r.GetOrdinal(c)));

        return new ProspectActivity
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            BusinessId = r.GetInt32(r.GetOrdinal("BusinessId")),
            ProspectId = r.GetInt32(r.GetOrdinal("ProspectId")),
            ActivityType = r.GetByte(r.GetOrdinal("ActivityType")),
            OccurredAtUtc = r.GetDateTime(r.GetOrdinal("OccurredAtUtc")),
            PerformedByUserId = S("PerformedByUserId"),
            Sentiment = B("Sentiment"),
            IsFollowUp = !r.IsDBNull(r.GetOrdinal("IsFollowUp")) && r.GetBoolean(r.GetOrdinal("IsFollowUp")),
            Outcome = S("Outcome"),
            Notes = S("Notes"),
            NextAction = S("NextAction"),
            NextActionDate = D("NextActionDate"),
            NextActionTime = T("NextActionTime"),
            NextActionContactName = S("NextActionContactName"),
            NextActionPhone = S("NextActionPhone"),
            CreatedAtUtc = r.GetDateTime(r.GetOrdinal("CreatedAtUtc"))
        };
    }

    /// <summary>
    /// Recent activity across ALL prospects in a campaign, newest first, capped at <paramref name="take"/>.
    /// Joins the prospect for its name so the campaign dashboard feed avoids an N+1. Returns lightweight
    /// rows (not full entities) since the feed only needs a few fields.
    /// </summary>
    public async Task<List<CampaignActivityFeedRow>> GetRecentByCampaignAsync(int campaignId, int businessId, int take)
    {
        try
        {
            var query = $@"
                SELECT TOP (@Take)
                       [act].[ProspectId], [pr].[Name] AS [ProspectName], [act].[ActivityType],
                       [act].[Outcome], [act].[OccurredAtUtc], [act].[Sentiment], [act].[IsFollowUp]
                FROM [sales].[ProspectActivity] AS [act]
                INNER JOIN [sales].[Prospect] AS [pr] ON [pr].[Id] = [act].[ProspectId]
                WHERE [act].[BusinessId] = @BusinessId AND [pr].[ProspectCampaignId] = @CampaignId
                ORDER BY [act].[OccurredAtUtc] DESC, [act].[Id] DESC";

            var result = new List<CampaignActivityFeedRow>();
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

                command.Parameters.Add(new SqlParameter("@Take", take));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@CampaignId", campaignId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new CampaignActivityFeedRow
                    {
                        ProspectId = reader.GetInt32(0),
                        ProspectName = reader.GetString(1),
                        ActivityType = reader.GetByte(2),
                        Outcome = reader.IsDBNull(3) ? null : reader.GetString(3),
                        OccurredAtUtc = reader.GetDateTime(4),
                        Sentiment = reader.IsDBNull(5) ? null : reader.GetByte(5),
                        IsFollowUp = !reader.IsDBNull(6) && reader.GetBoolean(6)
                    });
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

    // ─── Prospect Activity View (paged, filtered log + summary + cities) ─────────────────────────

    // Builds the shared WHERE clause + parameters for the activity log and its summary, so both apply
    // identical filtering. Full table names are used (no short aliases) per the SQL steering.
    private static string BuildActivityLogWhere(int businessId, ProspectActivityFilter f, List<SqlParameter> p)
    {
        var where = "[sales].[ProspectActivity].[BusinessId] = @BusinessId";
        p.Add(new SqlParameter("@BusinessId", businessId));

        if (f.CampaignId.HasValue)
        {
            where += " AND [sales].[Prospect].[ProspectCampaignId] = @CampaignId";
            p.Add(new SqlParameter("@CampaignId", f.CampaignId.Value));
        }
        if (f.ActivityType.HasValue)
        {
            where += " AND [sales].[ProspectActivity].[ActivityType] = @ActivityType";
            p.Add(new SqlParameter("@ActivityType", f.ActivityType.Value));
        }
        if (f.Sentiment.HasValue)
        {
            where += " AND [sales].[ProspectActivity].[Sentiment] = @Sentiment";
            p.Add(new SqlParameter("@Sentiment", f.Sentiment.Value));
        }
        if (!string.IsNullOrWhiteSpace(f.City))
        {
            where += " AND [sales].[Prospect].[Location] = @City";
            p.Add(new SqlParameter("@City", f.City.Trim()));
        }
        if (f.DateFrom.HasValue)
        {
            where += " AND CAST([sales].[ProspectActivity].[OccurredAtUtc] AS DATE) >= @DateFrom";
            p.Add(new SqlParameter("@DateFrom", f.DateFrom.Value.ToDateTime(TimeOnly.MinValue)));
        }
        if (f.DateTo.HasValue)
        {
            where += " AND CAST([sales].[ProspectActivity].[OccurredAtUtc] AS DATE) <= @DateTo";
            p.Add(new SqlParameter("@DateTo", f.DateTo.Value.ToDateTime(TimeOnly.MinValue)));
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            where += @" AND (
                [sales].[Prospect].[Name] LIKE @Search
                OR [sales].[ProspectActivity].[Outcome] LIKE @Search
                OR [sales].[ProspectActivity].[Notes] LIKE @Search
                OR [sales].[ProspectActivity].[NextAction] LIKE @Search)";
            p.Add(new SqlParameter("@Search", "%" + f.Search.Trim() + "%"));
        }
        return where;
    }

    /// <summary>
    /// Paged, filtered prospect activity log joined to the prospect and campaign. Tenant-scoped.
    /// Ordered newest-first by OccurredAtUtc. Returns the page rows plus the total matching count.
    /// </summary>
    public async Task<(List<ProspectActivityLogRow> Rows, int TotalCount)> GetActivityLogPagedAsync(
        int businessId, ProspectActivityFilter filter, int page, int pageSize)
    {
        try
        {
            var parameters = new List<SqlParameter>();
            var where = BuildActivityLogWhere(businessId, filter, parameters);

            var fromJoin = @"
                FROM [sales].[ProspectActivity]
                INNER JOIN [sales].[Prospect] ON [sales].[Prospect].[Id] = [sales].[ProspectActivity].[ProspectId]
                INNER JOIN [sales].[ProspectCampaign] ON [sales].[ProspectCampaign].[Id] = [sales].[Prospect].[ProspectCampaignId]";

            var countQuery = $"SELECT COUNT(*) {fromJoin} WHERE {where}";

            var dataQuery = $@"
                SELECT [sales].[ProspectActivity].[Id] AS [ActivityId],
                       [sales].[Prospect].[Id] AS [ProspectId],
                       [sales].[Prospect].[Name] AS [ProspectName],
                       [sales].[Prospect].[Location] AS [City],
                       [sales].[Prospect].[Segment] AS [Segment],
                       [sales].[Prospect].[ProspectCampaignId] AS [ProspectCampaignId],
                       [sales].[ProspectCampaign].[Name] AS [CampaignName],
                       [sales].[ProspectActivity].[ActivityType] AS [ActivityType],
                       [sales].[ProspectActivity].[OccurredAtUtc] AS [OccurredAtUtc],
                       [sales].[ProspectActivity].[Sentiment] AS [Sentiment],
                       [sales].[ProspectActivity].[IsFollowUp] AS [IsFollowUp],
                       [sales].[ProspectActivity].[Outcome] AS [Outcome],
                       [sales].[ProspectActivity].[NextAction] AS [NextAction],
                       [sales].[ProspectActivity].[NextActionDate] AS [NextActionDate],
                       [sales].[ProspectActivity].[NextActionTime] AS [NextActionTime],
                       [sales].[ProspectActivity].[NextActionContactName] AS [NextActionContactName],
                       [sales].[ProspectActivity].[NextActionPhone] AS [NextActionPhone],
                       [sales].[ProspectActivity].[PerformedByUserId] AS [PerformedByUserId]
                {fromJoin}
                WHERE {where}
                ORDER BY [sales].[ProspectActivity].[OccurredAtUtc] DESC, [sales].[ProspectActivity].[Id] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var rows = new List<ProspectActivityLogRow>();
            var connection = _context.Database.GetDbConnection();
            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _context.Database.CurrentTransaction;

                int totalCount;
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;
                    if (transaction != null) countCommand.Transaction = transaction.GetDbTransaction();
                    foreach (var prm in parameters)
                        countCommand.Parameters.Add(new SqlParameter(prm.ParameterName, prm.Value));
                    var countResult = await countCommand.ExecuteScalarAsync();
                    totalCount = countResult != null && countResult != DBNull.Value ? (int)countResult : 0;
                }

                if (totalCount == 0)
                    return (rows, 0);

                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page > totalPages) page = totalPages;
                if (page < 1) page = 1;
                var offset = (page - 1) * pageSize;

                using (var dataCommand = connection.CreateCommand())
                {
                    dataCommand.CommandText = dataQuery;
                    if (transaction != null) dataCommand.Transaction = transaction.GetDbTransaction();
                    foreach (var prm in parameters)
                        dataCommand.Parameters.Add(new SqlParameter(prm.ParameterName, prm.Value));
                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        rows.Add(ReadActivityLogRow(reader));
                }

                return (rows, totalCount);
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

    /// <summary>Aggregate counts for the SAME filtered set (ignores pagination).</summary>
    public async Task<ProspectActivitySummaryDto> GetActivitySummaryAsync(int businessId, ProspectActivityFilter filter)
    {
        try
        {
            var parameters = new List<SqlParameter>();
            var where = BuildActivityLogWhere(businessId, filter, parameters);

            var query = $@"
                SELECT
                    COUNT(*) AS [Total],
                    SUM(CASE WHEN [sales].[ProspectActivity].[ActivityType] = 2 THEN 1 ELSE 0 END) AS [Calls],
                    SUM(CASE WHEN [sales].[ProspectActivity].[ActivityType] = 3 THEN 1 ELSE 0 END) AS [Emails],
                    SUM(CASE WHEN [sales].[ProspectActivity].[ActivityType] = 4 THEN 1 ELSE 0 END) AS [Meetings],
                    SUM(CASE WHEN [sales].[ProspectActivity].[IsFollowUp] = 1 THEN 1 ELSE 0 END) AS [FollowUps],
                    SUM(CASE WHEN [sales].[ProspectActivity].[Sentiment] = 1 THEN 1 ELSE 0 END) AS [Positive],
                    SUM(CASE WHEN [sales].[ProspectActivity].[Sentiment] = 3 THEN 1 ELSE 0 END) AS [Negative]
                FROM [sales].[ProspectActivity]
                INNER JOIN [sales].[Prospect] ON [sales].[Prospect].[Id] = [sales].[ProspectActivity].[ProspectId]
                INNER JOIN [sales].[ProspectCampaign] ON [sales].[ProspectCampaign].[Id] = [sales].[Prospect].[ProspectCampaignId]
                WHERE {where}";

            var summary = new ProspectActivitySummaryDto();
            var connection = _context.Database.GetDbConnection();
            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();
                foreach (var prm in parameters)
                    command.Parameters.Add(new SqlParameter(prm.ParameterName, prm.Value));

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int GetI(string c) => reader.IsDBNull(reader.GetOrdinal(c)) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal(c)));
                    summary.Total = GetI("Total");
                    summary.Calls = GetI("Calls");
                    summary.Emails = GetI("Emails");
                    summary.Meetings = GetI("Meetings");
                    summary.FollowUps = GetI("FollowUps");
                    summary.Positive = GetI("Positive");
                    summary.Negative = GetI("Negative");
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
            return summary;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Distinct non-empty prospect cities for the City filter, within an optional campaign scope.</summary>
    public async Task<List<string>> GetDistinctCitiesAsync(int businessId, int? campaignId)
    {
        try
        {
            var where = "[sales].[Prospect].[BusinessId] = @BusinessId AND [sales].[Prospect].[Location] IS NOT NULL AND LTRIM(RTRIM([sales].[Prospect].[Location])) <> ''";
            var parameters = new List<SqlParameter> { new SqlParameter("@BusinessId", businessId) };
            if (campaignId.HasValue)
            {
                where += " AND [sales].[Prospect].[ProspectCampaignId] = @CampaignId";
                parameters.Add(new SqlParameter("@CampaignId", campaignId.Value));
            }

            var query = $@"
                SELECT DISTINCT [sales].[Prospect].[Location]
                FROM [sales].[Prospect]
                WHERE {where}
                ORDER BY [sales].[Prospect].[Location] ASC";

            var cities = new List<string>();
            var connection = _context.Database.GetDbConnection();
            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();
                foreach (var prm in parameters)
                    command.Parameters.Add(new SqlParameter(prm.ParameterName, prm.Value));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        cities.Add(reader.GetString(0));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
            return cities;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static ProspectActivityLogRow ReadActivityLogRow(System.Data.Common.DbDataReader r)
    {
        string? S(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : r.GetString(r.GetOrdinal(c));
        DateOnly? D(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal(c)));
        byte? B(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : r.GetByte(r.GetOrdinal(c));
        TimeOnly? T(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : TimeOnly.FromTimeSpan((TimeSpan)r.GetValue(r.GetOrdinal(c)));

        return new ProspectActivityLogRow
        {
            ActivityId = r.GetInt32(r.GetOrdinal("ActivityId")),
            ProspectId = r.GetInt32(r.GetOrdinal("ProspectId")),
            ProspectName = r.GetString(r.GetOrdinal("ProspectName")),
            City = S("City"),
            Segment = S("Segment"),
            ProspectCampaignId = r.GetInt32(r.GetOrdinal("ProspectCampaignId")),
            CampaignName = r.GetString(r.GetOrdinal("CampaignName")),
            ActivityType = r.GetByte(r.GetOrdinal("ActivityType")),
            OccurredAtUtc = r.GetDateTime(r.GetOrdinal("OccurredAtUtc")),
            Sentiment = B("Sentiment"),
            IsFollowUp = !r.IsDBNull(r.GetOrdinal("IsFollowUp")) && r.GetBoolean(r.GetOrdinal("IsFollowUp")),
            Outcome = S("Outcome"),
            NextAction = S("NextAction"),
            NextActionDate = D("NextActionDate"),
            NextActionTime = T("NextActionTime"),
            NextActionContactName = S("NextActionContactName"),
            NextActionPhone = S("NextActionPhone"),
            PerformedByUserId = S("PerformedByUserId")
        };
    }
}

/// <summary>Lightweight row for the campaign activity feed (repository projection).</summary>
public class CampaignActivityFeedRow
{
    public int ProspectId { get; set; }
    public string ProspectName { get; set; } = null!;
    public byte ActivityType { get; set; }
    public string? Outcome { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public byte? Sentiment { get; set; }
    public bool IsFollowUp { get; set; }
}
