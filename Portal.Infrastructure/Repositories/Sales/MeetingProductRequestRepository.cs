using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[MeetingProductRequest] entity operations.
/// </summary>
public class MeetingProductRequestRepository : GenericStoredProcedureRepository<MeetingProductRequest>
{
    public MeetingProductRequestRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(MeetingProductRequest entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[MeetingProductRequest]
                    ([MeetingId], [ProductId], [RequestText], [IsActive], [IsCancelled], [CreatedAtUtc])
                VALUES
                    (@MeetingId, @ProductId, @RequestText, 1, 0, @CreatedAtUtc);
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

                command.Parameters.Add(new SqlParameter("@MeetingId", entity.MeetingId));
                command.Parameters.Add(new SqlParameter("@ProductId", entity.ProductId));
                command.Parameters.Add(new SqlParameter("@RequestText", entity.RequestText ?? (object)DBNull.Value));
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

    public async Task<List<MeetingProductRequest>> GetByMeetingIdAsync(int meetingId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [MeetingId], [ProductId], [RequestText],
                       [IsActive], [IsCancelled], [CancellationTimestamp],
                       [CancellationDescription], [CreatedAtUtc]
                FROM [sales].[MeetingProductRequest]
                WHERE [MeetingId] = @MeetingId AND [IsActive] = 1";

            var results = await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@MeetingId", meetingId));
            return results.OrderByDescending(x => x.CreatedAtUtc).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
