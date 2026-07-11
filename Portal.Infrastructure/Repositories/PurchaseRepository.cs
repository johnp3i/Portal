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
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId], [PurchaseTypeId],
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
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId], [PurchaseTypeId],
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
                    ([BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId], [PurchaseTypeId],
                     [InvoiceNumber], [InvoiceDate], [Description],
                     [AmountExcludingVat], [VatAmount], [TotalAmount],
                     [Country], [Notes], [CreatedAtUtc], [UpdatedAtUtc])
                VALUES
                    (@BusinessId, @SupplierId, @ExpenseCategoryId, @PurchaseOriginTypeId, @PurchaseTypeId,
                     @InvoiceNumber, @InvoiceDate, @Description,
                     @AmountExcludingVat, @VatAmount, @TotalAmount,
                     @Country, @Notes, @CreatedAtUtc, @UpdatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@SupplierId", entity.SupplierId),
                new SqlParameter("@ExpenseCategoryId", entity.ExpenseCategoryId),
                new SqlParameter("@PurchaseOriginTypeId", entity.PurchaseOriginTypeId),
                new SqlParameter("@PurchaseTypeId", entity.PurchaseTypeId),
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
                    [PurchaseTypeId] = @PurchaseTypeId,
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
                new SqlParameter("@PurchaseTypeId", entity.PurchaseTypeId),
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
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId], [PurchaseTypeId],
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

    public virtual async Task<decimal> GetAnnualSpendingAsync(int businessId, int expenseCategoryId, int year, int? excludePurchaseId)
    {
        try
        {
            var sql = @"
                SELECT ISNULL(SUM([purchase].[Purchase].[TotalAmount]), 0)
                FROM [purchase].[Purchase]
                WHERE [purchase].[Purchase].[IsCancelled] = 0
                  AND [purchase].[Purchase].[BusinessId] = @BusinessId
                  AND [purchase].[Purchase].[ExpenseCategoryId] = @ExpenseCategoryId
                  AND YEAR([purchase].[Purchase].[InvoiceDate]) = @Year";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@ExpenseCategoryId", expenseCategoryId),
                new SqlParameter("@Year", year)
            };

            if (excludePurchaseId.HasValue)
            {
                sql += " AND [purchase].[Purchase].[Id] != @ExcludePurchaseId";
                parameters.Add(new SqlParameter("@ExcludePurchaseId", excludePurchaseId.Value));
            }

            var result = await _context.Database
                .SqlQueryRaw<decimal>(sql, parameters.ToArray())
                .ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<decimal> GetPeriodSpendingAsync(int businessId, int expenseCategoryId, DateOnly periodStart, DateOnly periodEnd, int? excludePurchaseId)
    {
        try
        {
            var sql = @"
                SELECT ISNULL(SUM([purchase].[Purchase].[TotalAmount]), 0)
                FROM [purchase].[Purchase]
                WHERE [purchase].[Purchase].[IsCancelled] = 0
                  AND [purchase].[Purchase].[BusinessId] = @BusinessId
                  AND [purchase].[Purchase].[ExpenseCategoryId] = @ExpenseCategoryId
                  AND [purchase].[Purchase].[InvoiceDate] BETWEEN @PeriodStart AND @PeriodEnd";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@ExpenseCategoryId", expenseCategoryId),
                new SqlParameter("@PeriodStart", periodStart),
                new SqlParameter("@PeriodEnd", periodEnd)
            };

            if (excludePurchaseId.HasValue)
            {
                sql += " AND [purchase].[Purchase].[Id] != @ExcludePurchaseId";
                parameters.Add(new SqlParameter("@ExcludePurchaseId", excludePurchaseId.Value));
            }

            var result = await _context.Database
                .SqlQueryRaw<decimal>(sql, parameters.ToArray())
                .ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<int> CountQualifyingPurchasesAsync(int businessId, int supplierId, int? expenseCategoryId, DateOnly startDate, DateOnly endDate)
    {
        try
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM [purchase].[Purchase]
                WHERE [purchase].[Purchase].[BusinessId] = @BusinessId
                  AND [purchase].[Purchase].[SupplierId] = @SupplierId
                  AND [purchase].[Purchase].[IsCancelled] = 0
                  AND (@ExpenseCategoryId IS NULL OR [purchase].[Purchase].[ExpenseCategoryId] = @ExpenseCategoryId)
                  AND [purchase].[Purchase].[InvoiceDate] >= @StartDate
                  AND [purchase].[Purchase].[InvoiceDate] <= @EndDate";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@SupplierId", supplierId),
                new SqlParameter("@ExpenseCategoryId", expenseCategoryId ?? (object)DBNull.Value),
                new SqlParameter("@StartDate", startDate),
                new SqlParameter("@EndDate", endDate)
            };

            var result = await _context.Database
                .SqlQueryRaw<int>(sql, parameters)
                .ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<int> CountAmountMatchingPurchasesAsync(int businessId, int supplierId, int? expenseCategoryId, DateOnly startDate, DateOnly endDate, decimal expectedAmount, decimal tolerancePercent)
    {
        try
        {
            var lowerBound = expectedAmount * (1 - tolerancePercent / 100);
            var upperBound = expectedAmount * (1 + tolerancePercent / 100);

            const string sql = @"
                SELECT COUNT(*)
                FROM [purchase].[Purchase]
                WHERE [purchase].[Purchase].[BusinessId] = @BusinessId
                  AND [purchase].[Purchase].[SupplierId] = @SupplierId
                  AND [purchase].[Purchase].[IsCancelled] = 0
                  AND (@ExpenseCategoryId IS NULL OR [purchase].[Purchase].[ExpenseCategoryId] = @ExpenseCategoryId)
                  AND [purchase].[Purchase].[InvoiceDate] >= @StartDate
                  AND [purchase].[Purchase].[InvoiceDate] <= @EndDate
                  AND [purchase].[Purchase].[AmountExcludingVat] >= @LowerBound
                  AND [purchase].[Purchase].[AmountExcludingVat] <= @UpperBound";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@SupplierId", supplierId),
                new SqlParameter("@ExpenseCategoryId", expenseCategoryId ?? (object)DBNull.Value),
                new SqlParameter("@StartDate", startDate),
                new SqlParameter("@EndDate", endDate),
                new SqlParameter("@LowerBound", lowerBound),
                new SqlParameter("@UpperBound", upperBound)
            };

            var result = await _context.Database
                .SqlQueryRaw<int>(sql, parameters)
                .ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
