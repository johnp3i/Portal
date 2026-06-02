using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PromoCodeRedemption entity insert operations against the [dbo].[PromoCodeRedemption] table.
/// </summary>
public class PromoCodeRedemptionRepository : GenericStoredProcedureRepository<PromoCodeRedemption>
{
    public PromoCodeRedemptionRepository(PortalDbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new PromoCodeRedemption record and returns the new Id via OUTPUT INSERTED.Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(PromoCodeRedemption entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [dbo].[PromoCodeRedemption]
                    ([PromoCodeId], [UserId], [BusinessId], [RedeemedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@PromoCodeId, @UserId, @BusinessId, @RedeemedAtUtc)";

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

                command.Parameters.Add(new SqlParameter("@PromoCodeId", entity.PromoCodeId));
                command.Parameters.Add(new SqlParameter("@UserId", entity.UserId));
                command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
                command.Parameters.Add(new SqlParameter("@RedeemedAtUtc", entity.RedeemedAtUtc));

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
