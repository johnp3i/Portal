using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Purchase entity CRUD operations against the [purchase].[Purchase] table.
/// </summary>
public class PurchaseRepository : GenericStoredProcedureRepository<Purchase>
{
    public PurchaseRepository(DbContext context) : base(context) { }

    public async Task<List<Purchase>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId],
                       [InvoiceNumber], [InvoiceDate], [Description],
                       [AmountExcludingVat], [VatAmount], [TotalAmount],
                       [Country], [Notes], [IsCancelled], [CancelledAtUtc], [VatSubmissionPeriodId], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [purchase].[Purchase]
                WHERE Purchase.BusinessId = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Purchase?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId],
                       [InvoiceNumber], [InvoiceDate], [Description],
                       [AmountExcludingVat], [VatAmount], [TotalAmount],
                       [Country], [Notes], [IsCancelled], [CancelledAtUtc], [VatSubmissionPeriodId], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [purchase].[Purchase]
                WHERE Purchase.Id = @Id AND Purchase.BusinessId = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task InsertAsync(Purchase entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [purchase].[Purchase]
                    ([BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId],
                     [InvoiceNumber], [InvoiceDate], [Description],
                     [AmountExcludingVat], [VatAmount], [TotalAmount],
                     [Country], [Notes], [CreatedAtUtc], [UpdatedAtUtc])
                VALUES
                    (@BusinessId, @SupplierId, @ExpenseCategoryId, @PurchaseOriginTypeId,
                     @InvoiceNumber, @InvoiceDate, @Description,
                     @AmountExcludingVat, @VatAmount, @TotalAmount,
                     @Country, @Notes, @CreatedAtUtc, @UpdatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@SupplierId", entity.SupplierId),
                new SqlParameter("@ExpenseCategoryId", entity.ExpenseCategoryId),
                new SqlParameter("@PurchaseOriginTypeId", entity.PurchaseOriginTypeId),
                new SqlParameter("@InvoiceNumber", entity.InvoiceNumber ?? (object)DBNull.Value),
                new SqlParameter("@InvoiceDate", entity.InvoiceDate),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@AmountExcludingVat", entity.AmountExcludingVat),
                new SqlParameter("@VatAmount", entity.VatAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount),
                new SqlParameter("@Country", entity.Country ?? (object)DBNull.Value),
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

    public async Task UpdateAsync(Purchase entity)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[Purchase]
                SET
                    [SupplierId] = @SupplierId,
                    [ExpenseCategoryId] = @ExpenseCategoryId,
                    [PurchaseOriginTypeId] = @PurchaseOriginTypeId,
                    [InvoiceNumber] = @InvoiceNumber,
                    [InvoiceDate] = @InvoiceDate,
                    [Description] = @Description,
                    [AmountExcludingVat] = @AmountExcludingVat,
                    [VatAmount] = @VatAmount,
                    [TotalAmount] = @TotalAmount,
                    [Country] = @Country,
                    [Notes] = @Notes,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE Purchase.Id = @Id AND Purchase.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@SupplierId", entity.SupplierId),
                new SqlParameter("@ExpenseCategoryId", entity.ExpenseCategoryId),
                new SqlParameter("@PurchaseOriginTypeId", entity.PurchaseOriginTypeId),
                new SqlParameter("@InvoiceNumber", entity.InvoiceNumber ?? (object)DBNull.Value),
                new SqlParameter("@InvoiceDate", entity.InvoiceDate),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@AmountExcludingVat", entity.AmountExcludingVat),
                new SqlParameter("@VatAmount", entity.VatAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount),
                new SqlParameter("@Country", entity.Country ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task CancelAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[Purchase]
                SET [IsCancelled] = 1,
                    [CancelledAtUtc] = GETUTCDATE()
                WHERE Purchase.Id = @Id AND Purchase.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<Purchase>> GetFilteredAsync(
        int businessId,
        int? supplierId,
        int? expenseCategoryId,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        try
        {
            var sql = @"
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId],
                       [InvoiceNumber], [InvoiceDate], [Description],
                       [AmountExcludingVat], [VatAmount], [TotalAmount],
                       [Country], [Notes], [IsCancelled], [CancelledAtUtc], [VatSubmissionPeriodId], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [purchase].[Purchase]
                WHERE Purchase.BusinessId = @BusinessId";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId)
            };

            if (supplierId.HasValue)
            {
                sql += " AND Purchase.SupplierId = @SupplierId";
                parameters.Add(new SqlParameter("@SupplierId", supplierId.Value));
            }

            if (expenseCategoryId.HasValue)
            {
                sql += " AND Purchase.ExpenseCategoryId = @ExpenseCategoryId";
                parameters.Add(new SqlParameter("@ExpenseCategoryId", expenseCategoryId.Value));
            }

            if (dateFrom.HasValue)
            {
                sql += " AND Purchase.InvoiceDate >= @DateFrom";
                parameters.Add(new SqlParameter("@DateFrom", dateFrom.Value));
            }

            if (dateTo.HasValue)
            {
                sql += " AND Purchase.InvoiceDate <= @DateTo";
                parameters.Add(new SqlParameter("@DateTo", dateTo.Value));
            }

            return await ExecuteStoredProcedure(sql, parameters.ToArray());
        }
        catch (Exception)
        {
            throw;
        }
    }
}
