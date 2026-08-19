using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[LeadResponse] entity operations.
/// </summary>
public class LeadResponseRepository : GenericStoredProcedureRepository<LeadResponse>
{
    public LeadResponseRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(LeadResponse entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[LeadResponse]
                    ([LeadRequestId], [LeadResponseTypeId], [LeadResponseTemplateId],
                     [RespondedByUserId], [ResponseText], [IsAutomated], [SentAtUtc], [CreatedAtUtc])
                VALUES
                    (@LeadRequestId, @LeadResponseTypeId, @LeadResponseTemplateId,
                     @RespondedByUserId, @ResponseText, @IsAutomated, @SentAtUtc, @CreatedAtUtc);
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

                command.Parameters.Add(new SqlParameter("@LeadRequestId", entity.LeadRequestId));
                command.Parameters.Add(new SqlParameter("@LeadResponseTypeId", entity.LeadResponseTypeId));
                command.Parameters.Add(new SqlParameter("@LeadResponseTemplateId", entity.LeadResponseTemplateId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@RespondedByUserId", entity.RespondedByUserId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ResponseText", entity.ResponseText ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsAutomated", entity.IsAutomated));
                command.Parameters.Add(new SqlParameter("@SentAtUtc", entity.SentAtUtc));
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

    public async Task<List<LeadResponse>> GetByLeadRequestIdAsync(int leadRequestId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [LeadRequestId], [LeadResponseTypeId], [LeadResponseTemplateId],
                       [RespondedByUserId], [ResponseText], [IsAutomated], [SentAtUtc], [CreatedAtUtc]
                FROM [sales].[LeadResponse]
                WHERE [LeadRequestId] = @LeadRequestId";

            var results = await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@LeadRequestId", leadRequestId));
            return results.OrderByDescending(x => x.SentAtUtc).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<Dictionary<int, DateTime>> GetEarliestResponseDatesAsync(List<int> leadRequestIds, int businessId)
    {
        try
        {
            if (leadRequestIds == null || leadRequestIds.Count == 0)
                return new Dictionary<int, DateTime>();

            var parameters = new List<SqlParameter>();
            var placeholders = new List<string>();
            for (int i = 0; i < leadRequestIds.Count; i++)
            {
                var paramName = $"@LeadRequestId{i}";
                placeholders.Add(paramName);
                parameters.Add(new SqlParameter(paramName, leadRequestIds[i]));
            }

            parameters.Add(new SqlParameter("@BusinessId", businessId));

            var query = $@"
                SELECT [sales].[LeadResponse].[LeadRequestId],
                       MIN([sales].[LeadResponse].[SentAtUtc]) AS [EarliestSentAtUtc]
                FROM [sales].[LeadResponse]
                INNER JOIN [sales].[LeadRequest]
                    ON [sales].[LeadResponse].[LeadRequestId] = [sales].[LeadRequest].[Id]
                WHERE [sales].[LeadResponse].[LeadRequestId] IN ({string.Join(", ", placeholders)})
                  AND [sales].[LeadRequest].[BusinessId] = @BusinessId
                GROUP BY [sales].[LeadResponse].[LeadRequestId]";

            var result = new Dictionary<int, DateTime>();
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

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var leadRequestId = reader.GetInt32(0);
                    var earliestSentAtUtc = reader.GetDateTime(1);
                    result[leadRequestId] = earliestSentAtUtc;
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
