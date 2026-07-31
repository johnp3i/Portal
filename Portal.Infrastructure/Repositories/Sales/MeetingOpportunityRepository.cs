using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[MeetingOpportunity] entity operations.
/// </summary>
public class MeetingOpportunityRepository : GenericStoredProcedureRepository<MeetingOpportunity>
{
    public MeetingOpportunityRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(MeetingOpportunity entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[MeetingOpportunity]
                    ([MeetingId], [Title], [Description], [EstimatedValue], [IsActive], [CreatedAtUtc])
                VALUES
                    (@MeetingId, @Title, @Description, @EstimatedValue, 1, @CreatedAtUtc);
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
                command.Parameters.Add(new SqlParameter("@Title", entity.Title));
                command.Parameters.Add(new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@EstimatedValue", entity.EstimatedValue ?? (object)DBNull.Value));
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

    public async Task<List<MeetingOpportunity>> GetByMeetingIdAsync(int meetingId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [MeetingId], [Title], [Description],
                       [EstimatedValue], [IsActive], [CreatedAtUtc]
                FROM [sales].[MeetingOpportunity]
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
