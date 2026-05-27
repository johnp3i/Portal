using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for CreditNoteLine entity CRUD operations against the [credit].[CreditNoteLine] table.
/// </summary>
public class CreditNoteLineRepository : GenericStoredProcedureRepository<CreditNoteLine>
{
    public CreditNoteLineRepository(DbContext context) : base(context) { }

    public async Task InsertBatchAsync(List<CreditNoteLine> lines)
    {
        try
        {
            const string query = @"
                INSERT INTO [credit].[CreditNoteLine]
                    ([CreditNoteId], [Description], [Quantity], [UnitPrice], [VatRate], [LineTotal], [SortOrder])
                VALUES
                    (@CreditNoteId, @Description, @Quantity, @UnitPrice, @VatRate, @LineTotal, @SortOrder)";

            foreach (var line in lines)
            {
                await _context.Database.ExecuteSqlRawAsync(query,
                    new SqlParameter("@CreditNoteId", line.CreditNoteId),
                    new SqlParameter("@Description", line.Description),
                    new SqlParameter("@Quantity", line.Quantity),
                    new SqlParameter("@UnitPrice", line.UnitPrice),
                    new SqlParameter("@VatRate", line.VatRate),
                    new SqlParameter("@LineTotal", line.LineTotal),
                    new SqlParameter("@SortOrder", line.SortOrder)
                );
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<CreditNoteLine>> GetByCreditNoteIdAsync(int creditNoteId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [CreditNoteId], [Description], [Quantity], [UnitPrice], [VatRate], [LineTotal], [SortOrder]
                FROM [credit].[CreditNoteLine]
                WHERE [CreditNoteId] = @CreditNoteId
                ORDER BY [SortOrder]";

            return await ExecuteStoredProcedure(query, new SqlParameter("@CreditNoteId", creditNoteId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeleteByCreditNoteIdAsync(int creditNoteId)
    {
        try
        {
            const string query = @"
                DELETE FROM [credit].[CreditNoteLine]
                WHERE [CreditNoteId] = @CreditNoteId";

            await _context.Database.ExecuteSqlRawAsync(query, new SqlParameter("@CreditNoteId", creditNoteId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
