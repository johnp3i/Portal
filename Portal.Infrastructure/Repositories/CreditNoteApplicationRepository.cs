using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for CreditNoteApplication entity CRUD operations against the [credit].[CreditNoteApplication] table.
/// </summary>
public class CreditNoteApplicationRepository : GenericStoredProcedureRepository<CreditNoteApplication>
{
    public CreditNoteApplicationRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new credit note application record and returns the new Id via OUTPUT INSERTED.Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(CreditNoteApplication entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [credit].[CreditNoteApplication]
                    ([CreditNoteId], [InvoiceId], [AmountApplied], [AppliedAtUtc],
                     [AppliedByUserId], [IsVoided], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@CreditNoteId, @InvoiceId, @AmountApplied, @AppliedAtUtc,
                     @AppliedByUserId, @IsVoided, @CreatedAtUtc)";

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

                command.Parameters.Add(new SqlParameter("@CreditNoteId", entity.CreditNoteId));
                command.Parameters.Add(new SqlParameter("@InvoiceId", entity.InvoiceId));
                command.Parameters.Add(new SqlParameter("@AmountApplied", entity.AmountApplied));
                command.Parameters.Add(new SqlParameter("@AppliedAtUtc", entity.AppliedAtUtc));
                command.Parameters.Add(new SqlParameter("@AppliedByUserId", entity.AppliedByUserId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsVoided", entity.IsVoided));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc));

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

    /// <summary>
    /// Gets all credit note application records for a given credit note.
    /// </summary>
    public virtual async Task<List<CreditNoteApplication>> GetByCreditNoteIdAsync(int creditNoteId)
    {
        try
        {
            const string query = @"
                SELECT [credit].[CreditNoteApplication].[Id],
                       [credit].[CreditNoteApplication].[CreditNoteId],
                       [credit].[CreditNoteApplication].[InvoiceId],
                       [credit].[CreditNoteApplication].[AmountApplied],
                       [credit].[CreditNoteApplication].[AppliedAtUtc],
                       [credit].[CreditNoteApplication].[AppliedByUserId],
                       [credit].[CreditNoteApplication].[IsVoided],
                       [credit].[CreditNoteApplication].[CreatedAtUtc]
                FROM [credit].[CreditNoteApplication]
                WHERE [credit].[CreditNoteApplication].[CreditNoteId] = @CreditNoteId";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@CreditNoteId", creditNoteId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Sets IsVoided = 1 on all application records for a given credit note.
    /// Used when voiding a previously applied credit note.
    /// </summary>
    public virtual async Task VoidByCreditNoteIdAsync(int creditNoteId)
    {
        try
        {
            const string query = @"
                UPDATE [credit].[CreditNoteApplication]
                SET [IsVoided] = 1
                WHERE [credit].[CreditNoteApplication].[CreditNoteId] = @CreditNoteId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@CreditNoteId", creditNoteId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
