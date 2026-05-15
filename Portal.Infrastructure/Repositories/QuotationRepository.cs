using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Quotation entity CRUD operations against the [quotation].[Quotation] table.
/// </summary>
public class QuotationRepository : GenericStoredProcedureRepository<Quotation>
{
    public QuotationRepository(DbContext context) : base(context) { }

    public async Task<List<Quotation>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [quotation].[Quotation].[Id], [quotation].[Quotation].[BusinessId], [quotation].[Quotation].[CustomerId],
                       [quotation].[Quotation].[QuotationStatusTypeId], [quotation].[Quotation].[Reference],
                       [quotation].[Quotation].[ValidUntil], [quotation].[Quotation].[Subtotal],
                       [quotation].[Quotation].[TaxAmount], [quotation].[Quotation].[TotalAmount],
                       [quotation].[Quotation].[Notes], [quotation].[Quotation].[CreatedAtUtc],
                       [quotation].[Quotation].[UpdatedAtUtc], [quotation].[Quotation].[QuotationContactId],
                       [quotation].[Quotation].[IsGrandTotalShown]
                FROM [quotation].[Quotation]
                WHERE [quotation].[Quotation].[BusinessId] = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Quotation?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [quotation].[Quotation].[Id], [quotation].[Quotation].[BusinessId], [quotation].[Quotation].[CustomerId],
                       [quotation].[Quotation].[QuotationStatusTypeId], [quotation].[Quotation].[Reference],
                       [quotation].[Quotation].[ValidUntil], [quotation].[Quotation].[Subtotal],
                       [quotation].[Quotation].[TaxAmount], [quotation].[Quotation].[TotalAmount],
                       [quotation].[Quotation].[Notes], [quotation].[Quotation].[CreatedAtUtc],
                       [quotation].[Quotation].[UpdatedAtUtc], [quotation].[Quotation].[QuotationContactId],
                       [quotation].[Quotation].[IsGrandTotalShown]
                FROM [quotation].[Quotation]
                WHERE [quotation].[Quotation].[Id] = @Id AND [quotation].[Quotation].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task InsertAsync(Quotation entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [quotation].[Quotation]
                    ([BusinessId], [CustomerId], [QuotationStatusTypeId], [Reference], [ValidUntil],
                     [Subtotal], [TaxAmount], [TotalAmount], [Notes], [CreatedAtUtc], [UpdatedAtUtc])
                VALUES
                    (@BusinessId, @CustomerId, @QuotationStatusTypeId, @Reference, @ValidUntil,
                     @Subtotal, @TaxAmount, @TotalAmount, @Notes, @CreatedAtUtc, @UpdatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@CustomerId", entity.CustomerId),
                new SqlParameter("@QuotationStatusTypeId", entity.QuotationStatusTypeId),
                new SqlParameter("@Reference", entity.Reference),
                new SqlParameter("@ValidUntil", entity.ValidUntil.HasValue ? entity.ValidUntil.Value : (object)DBNull.Value),
                new SqlParameter("@Subtotal", entity.Subtotal),
                new SqlParameter("@TaxAmount", entity.TaxAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateAsync(Quotation entity)
    {
        try
        {
            const string query = @"
                UPDATE [quotation].[Quotation]
                SET
                    [CustomerId] = @CustomerId,
                    [QuotationStatusTypeId] = @QuotationStatusTypeId,
                    [Reference] = @Reference,
                    [ValidUntil] = @ValidUntil,
                    [Subtotal] = @Subtotal,
                    [TaxAmount] = @TaxAmount,
                    [TotalAmount] = @TotalAmount,
                    [Notes] = @Notes,
                    [QuotationContactId] = @QuotationContactId,
                    [IsGrandTotalShown] = @IsGrandTotalShown,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@CustomerId", entity.CustomerId),
                new SqlParameter("@QuotationStatusTypeId", entity.QuotationStatusTypeId),
                new SqlParameter("@Reference", entity.Reference),
                new SqlParameter("@ValidUntil", entity.ValidUntil.HasValue ? entity.ValidUntil.Value : (object)DBNull.Value),
                new SqlParameter("@Subtotal", entity.Subtotal),
                new SqlParameter("@TaxAmount", entity.TaxAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@QuotationContactId", entity.QuotationContactId ?? (object)DBNull.Value),
                new SqlParameter("@IsGrandTotalShown", entity.IsGrandTotalShown),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<int> GetNextSequentialNumberAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(MAX([Id]), 0) + 1
                FROM [quotation].[Quotation]
                WHERE [BusinessId] = @BusinessId";

            var result = await _context.Database
                .SqlQueryRaw<int>(query, new SqlParameter("@BusinessId", businessId))
                .ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception)
        {
            throw;
        }
    }
}
