using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[ProspectCampaign] — prospecting campaigns.
/// </summary>
public class ProspectCampaignRepository : GenericStoredProcedureRepository<ProspectCampaign>
{
    // Full entity column list — FromSqlRaw materializes every mapped property, so all SELECTs
    // that map to ProspectCampaign must project this exact set.
    private const string Columns =
        "[Id], [BusinessId], [Name], [SalesProductId], [Description], [Market], " +
        "[StartDate], [EndDate], [WeeklyCallTarget], [OwnerUserId], [Status], [Notes], [CreatedAtUtc]";

    public ProspectCampaignRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(ProspectCampaign entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[ProspectCampaign]
                    ([BusinessId], [Name], [SalesProductId], [Description], [Market],
                     [StartDate], [EndDate], [WeeklyCallTarget], [OwnerUserId], [Status], [Notes], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @Name, @SalesProductId, @Description, @Market,
                     @StartDate, @EndDate, @WeeklyCallTarget, @OwnerUserId, @Status, @Notes, @CreatedAtUtc);
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
                command.Parameters.Add(new SqlParameter("@Name", entity.Name));
                command.Parameters.Add(new SqlParameter("@SalesProductId", entity.SalesProductId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Market", entity.Market ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@StartDate", entity.StartDate.HasValue ? entity.StartDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@EndDate", entity.EndDate.HasValue ? entity.EndDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@WeeklyCallTarget", entity.WeeklyCallTarget));
                command.Parameters.Add(new SqlParameter("@OwnerUserId", entity.OwnerUserId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Status", entity.Status));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
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

    public async Task UpdateAsync(ProspectCampaign entity)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[ProspectCampaign]
                SET [Name] = @Name,
                    [SalesProductId] = @SalesProductId,
                    [Description] = @Description,
                    [Market] = @Market,
                    [StartDate] = @StartDate,
                    [EndDate] = @EndDate,
                    [WeeklyCallTarget] = @WeeklyCallTarget,
                    [OwnerUserId] = @OwnerUserId,
                    [Status] = @Status,
                    [Notes] = @Notes
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@SalesProductId", entity.SalesProductId ?? (object)DBNull.Value),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@Market", entity.Market ?? (object)DBNull.Value),
                new SqlParameter("@StartDate", entity.StartDate.HasValue ? entity.StartDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value),
                new SqlParameter("@EndDate", entity.EndDate.HasValue ? entity.EndDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value),
                new SqlParameter("@WeeklyCallTarget", entity.WeeklyCallTarget),
                new SqlParameter("@OwnerUserId", entity.OwnerUserId ?? (object)DBNull.Value),
                new SqlParameter("@Status", entity.Status),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ProspectCampaign?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            var query = $@"
                SELECT {Columns}
                FROM [sales].[ProspectCampaign]
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

    public async Task<List<ProspectCampaign>> GetAllByBusinessAsync(int businessId)
    {
        try
        {
            // NOTE: no ORDER BY in the raw SQL. Because ProspectCampaign has a global query
            // filter, EF wraps this statement as a subquery and SQL Server rejects an ORDER BY
            // inside a subquery/derived table (unless TOP/OFFSET is present). Order in LINQ on
            // the composed IQueryable instead, so the sort lands on the outer query.
            var query = $@"
                SELECT {Columns}
                FROM [sales].[ProspectCampaign]
                WHERE [BusinessId] = @BusinessId";

            var results = await _context.Set<ProspectCampaign>()
                .FromSqlRaw(query, new SqlParameter("@BusinessId", businessId))
                .OrderBy(c => c.Status)
                .ThenByDescending(c => c.CreatedAtUtc)
                .ToListAsync();
            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns prospect counts grouped by prospect Status for one campaign, for the funnel.
    /// Key = prospect Status (1..6), Value = count.
    /// </summary>
    public async Task<Dictionary<byte, int>> GetFunnelCountsAsync(int campaignId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Status], COUNT(*) AS [Cnt]
                FROM [sales].[Prospect]
                WHERE [ProspectCampaignId] = @CampaignId AND [BusinessId] = @BusinessId
                GROUP BY [Status]";

            var result = new Dictionary<byte, int>();
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

                command.Parameters.Add(new SqlParameter("@CampaignId", campaignId));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result[(byte)reader.GetByte(0)] = reader.GetInt32(1);
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

    /// <summary>Count of prospects in a campaign whose NextActionDate is on or before the given date (follow-ups due).</summary>
    public async Task<int> GetFollowUpsDueCountAsync(int campaignId, int businessId, DateTime onOrBefore)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [sales].[Prospect]
                WHERE [ProspectCampaignId] = @CampaignId AND [BusinessId] = @BusinessId
                  AND [Status] NOT IN (5, 6)
                  AND [NextActionDate] IS NOT NULL
                  AND [NextActionDate] <= @OnOrBefore";

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

                command.Parameters.Add(new SqlParameter("@CampaignId", campaignId));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@OnOrBefore", onOrBefore.Date));

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? (int)result : 0;
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
}
