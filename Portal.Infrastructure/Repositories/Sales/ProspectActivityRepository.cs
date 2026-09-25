using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;

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
        "[Sentiment], [IsFollowUp], [Outcome], [Notes], [NextAction], [NextActionDate], [CreatedAtUtc]";

    public ProspectActivityRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(ProspectActivity entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[ProspectActivity]
                    ([BusinessId], [ProspectId], [ActivityType], [OccurredAtUtc], [PerformedByUserId],
                     [Sentiment], [IsFollowUp], [Outcome], [Notes], [NextAction], [NextActionDate], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @ProspectId, @ActivityType, @OccurredAtUtc, @PerformedByUserId,
                     @Sentiment, @IsFollowUp, @Outcome, @Notes, @NextAction, @NextActionDate, @CreatedAtUtc);
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
                    [NextActionDate] = @NextActionDate
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
                new SqlParameter("@NextActionDate", entity.NextActionDate.HasValue ? entity.NextActionDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value));
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
