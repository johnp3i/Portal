using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

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
                       [InvoiceNumber], [InvoiceDate], [SupplierDueDate], [TargetPaymentDate], [Description],
                       [AmountExcludingVat], [VatAmount], [TotalAmount],
                       [Country], [Notes], [IsCancelled], [CancelledAtUtc], [CancelledByUserId], [PayslipPeriodId], [VatSubmissionPeriodId], [IsPaid], [PaidAtUtc], [CreatedAtUtc], [UpdatedAtUtc]
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
                       [InvoiceNumber], [InvoiceDate], [SupplierDueDate], [TargetPaymentDate], [Description],
                       [AmountExcludingVat], [VatAmount], [TotalAmount],
                       [Country], [Notes], [IsCancelled], [CancelledAtUtc], [CancelledByUserId], [PayslipPeriodId], [VatSubmissionPeriodId], [IsPaid], [PaidAtUtc], [CreatedAtUtc], [UpdatedAtUtc]
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

    /// <summary>
    /// Gets the most recent non-cancelled purchase for a supplier, used to suggest a default
    /// expense category when recording a new purchase for the same supplier.
    /// </summary>
    public virtual async Task<Purchase?> GetMostRecentBySupplierAsync(int businessId, int supplierId)
    {
        try
        {
            const string query = @"
                SELECT TOP 1 [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId], [PurchaseTypeId],
                       [InvoiceNumber], [InvoiceDate], [SupplierDueDate], [TargetPaymentDate], [Description],
                       [AmountExcludingVat], [VatAmount], [TotalAmount],
                       [Country], [Notes], [IsCancelled], [CancelledAtUtc], [CancelledByUserId], [PayslipPeriodId], [VatSubmissionPeriodId], [IsPaid], [PaidAtUtc], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [purchase].[Purchase]
                WHERE Purchase.BusinessId = @BusinessId
                  AND Purchase.SupplierId = @SupplierId
                  AND Purchase.IsCancelled = 0
                ORDER BY Purchase.InvoiceDate DESC, Purchase.CreatedAtUtc DESC";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@SupplierId", supplierId));
        }
        catch (Exception ex)
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
                     [InvoiceNumber], [InvoiceDate], [SupplierDueDate], [TargetPaymentDate], [Description],
                     [AmountExcludingVat], [VatAmount], [TotalAmount],
                     [Country], [Notes], [PayslipPeriodId], [CreatedAtUtc], [UpdatedAtUtc])
                VALUES
                    (@BusinessId, @SupplierId, @ExpenseCategoryId, @PurchaseOriginTypeId, @PurchaseTypeId,
                     @InvoiceNumber, @InvoiceDate, @SupplierDueDate, @TargetPaymentDate, @Description,
                     @AmountExcludingVat, @VatAmount, @TotalAmount,
                     @Country, @Notes, @PayslipPeriodId, @CreatedAtUtc, @UpdatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@SupplierId", entity.SupplierId),
                new SqlParameter("@ExpenseCategoryId", entity.ExpenseCategoryId),
                new SqlParameter("@PurchaseOriginTypeId", entity.PurchaseOriginTypeId),
                new SqlParameter("@PurchaseTypeId", entity.PurchaseTypeId),
                new SqlParameter("@InvoiceNumber", entity.InvoiceNumber ?? (object)DBNull.Value),
                new SqlParameter("@InvoiceDate", entity.InvoiceDate),
                new SqlParameter("@SupplierDueDate", entity.SupplierDueDate ?? (object)DBNull.Value),
                new SqlParameter("@TargetPaymentDate", entity.TargetPaymentDate ?? (object)DBNull.Value),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@AmountExcludingVat", entity.AmountExcludingVat),
                new SqlParameter("@VatAmount", entity.VatAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount),
                new SqlParameter("@Country", entity.Country ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@PayslipPeriodId", entity.PayslipPeriodId ?? (object)DBNull.Value),
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
                    [SupplierDueDate] = @SupplierDueDate,
                    [TargetPaymentDate] = @TargetPaymentDate,
                    [Description] = @Description,
                    [AmountExcludingVat] = @AmountExcludingVat,
                    [VatAmount] = @VatAmount,
                    [TotalAmount] = @TotalAmount,
                    [Country] = @Country,
                    [Notes] = @Notes,
                    [VatSubmissionPeriodId] = @VatSubmissionPeriodId,
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
                new SqlParameter("@SupplierDueDate", entity.SupplierDueDate ?? (object)DBNull.Value),
                new SqlParameter("@TargetPaymentDate", entity.TargetPaymentDate ?? (object)DBNull.Value),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@AmountExcludingVat", entity.AmountExcludingVat),
                new SqlParameter("@VatAmount", entity.VatAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount),
                new SqlParameter("@Country", entity.Country ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@VatSubmissionPeriodId", entity.VatSubmissionPeriodId ?? (object)DBNull.Value),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
        }
        catch (Exception ex)
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
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task CancelWithUserAsync(int id, int businessId, string userId)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[Purchase]
                SET [IsCancelled] = 1,
                    [CancelledAtUtc] = GETUTCDATE(),
                    [CancelledByUserId] = @CancelledByUserId
                WHERE Purchase.Id = @Id AND Purchase.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@CancelledByUserId", userId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Sets the paid state. When marking paid, stamps PaidAtUtc = now; when marking unpaid, clears it.
    /// </summary>
    public async Task SetPaidStateAsync(int id, int businessId, bool isPaid)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[Purchase]
                SET [IsPaid] = @IsPaid,
                    [PaidAtUtc] = CASE WHEN @IsPaid = 1 THEN GETUTCDATE() ELSE NULL END
                WHERE Purchase.Id = @Id AND Purchase.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@IsPaid", isPaid));
        }
        catch (Exception ex)
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
                       [InvoiceNumber], [InvoiceDate], [SupplierDueDate], [TargetPaymentDate], [Description],
                       [AmountExcludingVat], [VatAmount], [TotalAmount],
                       [Country], [Notes], [IsCancelled], [CancelledAtUtc], [CancelledByUserId], [PayslipPeriodId], [VatSubmissionPeriodId], [IsPaid], [PaidAtUtc], [CreatedAtUtc], [UpdatedAtUtc]
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

    public virtual async Task<List<Purchase>> GetUnassignedByDateRangeAsync(int businessId, DateOnly startDate, DateOnly endDate)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId], [PurchaseTypeId],
                       [InvoiceNumber], [InvoiceDate], [SupplierDueDate], [TargetPaymentDate], [Description],
                       [AmountExcludingVat], [VatAmount], [TotalAmount],
                       [Country], [Notes], [IsCancelled], [CancelledAtUtc], [CancelledByUserId], [PayslipPeriodId], [VatSubmissionPeriodId], [IsPaid], [PaidAtUtc], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [purchase].[Purchase]
                WHERE [purchase].[Purchase].[BusinessId] = @BusinessId
                  AND [purchase].[Purchase].[VatSubmissionPeriodId] IS NULL
                  AND [purchase].[Purchase].[IsCancelled] = 0
                  AND [purchase].[Purchase].[InvoiceDate] >= @StartDate
                  AND [purchase].[Purchase].[InvoiceDate] <= @EndDate";

            var results = await ExecuteStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@StartDate", startDate),
                new SqlParameter("@EndDate", endDate));

            return results.OrderByDescending(p => p.InvoiceDate).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets non-cancelled purchases with an effective due date (TargetPaymentDate ?? SupplierDueDate)
    /// on or before the cutoff date, ordered by effective due date ascending.
    /// Used for the dashboard "Upcoming Supplier Payments" widget.
    /// </summary>
    public virtual async Task<List<Purchase>> GetUpcomingDueByBusinessIdAsync(int businessId, DateOnly cutoffDate)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId], [PurchaseTypeId],
                       [InvoiceNumber], [InvoiceDate], [SupplierDueDate], [TargetPaymentDate], [Description],
                       [AmountExcludingVat], [VatAmount], [TotalAmount],
                       [Country], [Notes], [IsCancelled], [CancelledAtUtc], [CancelledByUserId], [PayslipPeriodId], [VatSubmissionPeriodId], [IsPaid], [PaidAtUtc], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [purchase].[Purchase]
                WHERE [purchase].[Purchase].[BusinessId] = @BusinessId
                  AND [purchase].[Purchase].[IsCancelled] = 0
                  AND [purchase].[Purchase].[IsPaid] = 0
                  AND COALESCE([purchase].[Purchase].[TargetPaymentDate], [purchase].[Purchase].[SupplierDueDate]) IS NOT NULL
                  AND COALESCE([purchase].[Purchase].[TargetPaymentDate], [purchase].[Purchase].[SupplierDueDate]) <= @CutoffDate
                ORDER BY COALESCE([purchase].[Purchase].[TargetPaymentDate], [purchase].[Purchase].[SupplierDueDate]) ASC";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@CutoffDate", cutoffDate));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<int> CountUnassignedByDateRangeAsync(int businessId, DateOnly startDate, DateOnly endDate)
    {
        try
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM [purchase].[Purchase]
                WHERE [purchase].[Purchase].[BusinessId] = @BusinessId
                  AND [purchase].[Purchase].[VatSubmissionPeriodId] IS NULL
                  AND [purchase].[Purchase].[IsCancelled] = 0
                  AND [purchase].[Purchase].[InvoiceDate] >= @StartDate
                  AND [purchase].[Purchase].[InvoiceDate] <= @EndDate";

            var result = await _context.Database
                .SqlQueryRaw<int>(sql,
                    new SqlParameter("@BusinessId", businessId),
                    new SqlParameter("@StartDate", startDate),
                    new SqlParameter("@EndDate", endDate))
                .ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<int> BulkAssignToPeriodAsync(int businessId, int periodId, List<int> purchaseIds)
    {
        try
        {
            if (purchaseIds == null || purchaseIds.Count == 0)
                return 0;

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@PeriodId", periodId)
            };

            var idPlaceholders = new List<string>();
            for (int i = 0; i < purchaseIds.Count; i++)
            {
                var paramName = $"@Id{i}";
                idPlaceholders.Add(paramName);
                parameters.Add(new SqlParameter(paramName, purchaseIds[i]));
            }

            var sql = $@"
                UPDATE [purchase].[Purchase]
                SET [VatSubmissionPeriodId] = @PeriodId
                WHERE [purchase].[Purchase].[BusinessId] = @BusinessId
                  AND [purchase].[Purchase].[Id] IN ({string.Join(", ", idPlaceholders)})
                  AND [purchase].[Purchase].[IsCancelled] = 0
                  AND [purchase].[Purchase].[VatSubmissionPeriodId] IS NULL";

            return await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<int> BulkUnassignFromPeriodAsync(int businessId, List<int> purchaseIds)
    {
        try
        {
            if (purchaseIds == null || purchaseIds.Count == 0)
                return 0;

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId)
            };

            var idPlaceholders = new List<string>();
            for (int i = 0; i < purchaseIds.Count; i++)
            {
                var paramName = $"@Id{i}";
                idPlaceholders.Add(paramName);
                parameters.Add(new SqlParameter(paramName, purchaseIds[i]));
            }

            var sql = $@"
                UPDATE [purchase].[Purchase]
                SET [VatSubmissionPeriodId] = NULL
                WHERE [purchase].[Purchase].[BusinessId] = @BusinessId
                  AND [purchase].[Purchase].[Id] IN ({string.Join(", ", idPlaceholders)})
                  AND [purchase].[Purchase].[VatSubmissionPeriodId] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM [vat].[VatSubmission]
                      WHERE [vat].[VatSubmission].[VatSubmissionPeriodId] = [purchase].[Purchase].[VatSubmissionPeriodId]
                        AND [vat].[VatSubmission].[BusinessId] = @BusinessId
                        AND [vat].[VatSubmission].[IsSubmitted] = 1
                  )";

            return await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Purchases list â€” server-side paging, filtering, search and aggregates
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // Explicit Purchase column list (qualified) so FromSqlRaw can materialize the entity even when
    // the query joins other tables for filtering/search.
    private const string PurchaseColumns =
        "[purchase].[Purchase].[Id], [purchase].[Purchase].[BusinessId], [purchase].[Purchase].[SupplierId], " +
        "[purchase].[Purchase].[ExpenseCategoryId], [purchase].[Purchase].[PurchaseOriginTypeId], [purchase].[Purchase].[PurchaseTypeId], " +
        "[purchase].[Purchase].[InvoiceNumber], [purchase].[Purchase].[InvoiceDate], [purchase].[Purchase].[SupplierDueDate], " +
        "[purchase].[Purchase].[TargetPaymentDate], [purchase].[Purchase].[Description], [purchase].[Purchase].[AmountExcludingVat], " +
        "[purchase].[Purchase].[VatAmount], [purchase].[Purchase].[TotalAmount], [purchase].[Purchase].[Country], " +
        "[purchase].[Purchase].[Notes], [purchase].[Purchase].[IsCancelled], [purchase].[Purchase].[CancelledAtUtc], " +
        "[purchase].[Purchase].[CancelledByUserId], [purchase].[Purchase].[PayslipPeriodId], [purchase].[Purchase].[VatSubmissionPeriodId], " +
        "[purchase].[Purchase].[IsPaid], [purchase].[Purchase].[PaidAtUtc], [purchase].[Purchase].[CreatedAtUtc], [purchase].[Purchase].[UpdatedAtUtc]";

    // Builds the shared FROM + WHERE for the list, its count, and its aggregates so all three apply
    // identical filtering. A LEFT JOIN to Supplier lets the free-text search match the supplier name
    // in SQL (the previous in-memory search silently missed suppliers in the export paths and for
    // inactive suppliers). Search uses LIKE, which is case-insensitive under the default collation.
    private static string BuildListFromWhere(int businessId, PurchaseListFilter f, List<SqlParameter> p, bool includeVatJoin = false)
    {
        p.Add(new SqlParameter("@BusinessId", businessId));
        var where = "[purchase].[Purchase].[BusinessId] = @BusinessId";

        if (f.SupplierId.HasValue)
        {
            where += " AND [purchase].[Purchase].[SupplierId] = @SupplierId";
            p.Add(new SqlParameter("@SupplierId", f.SupplierId.Value));
        }
        if (f.ExpenseCategoryId.HasValue)
        {
            where += " AND [purchase].[Purchase].[ExpenseCategoryId] = @ExpenseCategoryId";
            p.Add(new SqlParameter("@ExpenseCategoryId", f.ExpenseCategoryId.Value));
        }
        if (f.PurchaseOriginTypeId.HasValue)
        {
            where += " AND [purchase].[Purchase].[PurchaseOriginTypeId] = @PurchaseOriginTypeId";
            p.Add(new SqlParameter("@PurchaseOriginTypeId", f.PurchaseOriginTypeId.Value));
        }
        if (f.PurchaseTypeId.HasValue)
        {
            where += " AND [purchase].[Purchase].[PurchaseTypeId] = @PurchaseTypeId";
            p.Add(new SqlParameter("@PurchaseTypeId", f.PurchaseTypeId.Value));
        }
        if (f.VatPeriodId.HasValue)
        {
            where += " AND [purchase].[Purchase].[VatSubmissionPeriodId] = @VatPeriodId";
            p.Add(new SqlParameter("@VatPeriodId", f.VatPeriodId.Value));
        }

        // Hide payroll-generated purchases (PayslipPeriodId != null) unless the caller opts in.
        // One predicate, shared by the page, count, export and aggregate queries.
        if (!f.IncludePayroll)
        {
            where += " AND [purchase].[Purchase].[PayslipPeriodId] IS NULL";
        }
        if (f.DateFrom.HasValue)
        {
            where += " AND [purchase].[Purchase].[InvoiceDate] >= @DateFrom";
            p.Add(new SqlParameter("@DateFrom", f.DateFrom.Value));
        }
        if (f.DateTo.HasValue)
        {
            where += " AND [purchase].[Purchase].[InvoiceDate] <= @DateTo";
            p.Add(new SqlParameter("@DateTo", f.DateTo.Value));
        }
        if (!string.IsNullOrWhiteSpace(f.SearchTerm))
        {
            where += @" AND (
                [purchase].[Purchase].[Description] LIKE @Search
                OR [purchase].[Purchase].[InvoiceNumber] LIKE @Search
                OR [purchase].[Supplier].[Name] LIKE @Search)";
            // Escape LIKE wildcards in user input so % and _ are treated literally.
            var safe = f.SearchTerm.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
            p.Add(new SqlParameter("@Search", "%" + safe + "%"));
        }

        var vatJoin = includeVatJoin
            ? "\n                 LEFT JOIN [vat].[VatSubmissionPeriod] ON [vat].[VatSubmissionPeriod].[Id] = [purchase].[Purchase].[VatSubmissionPeriodId]"
            : string.Empty;

        return @"FROM [purchase].[Purchase]
                 LEFT JOIN [purchase].[Supplier] ON [purchase].[Supplier].[Id] = [purchase].[Purchase].[SupplierId]" + vatJoin + @"
                 WHERE " + where;
    }

    private static string BuildOrderBy(string? sortBy) => (sortBy ?? "recent") switch
    {
        "date_desc" => "ORDER BY [purchase].[Purchase].[InvoiceDate] DESC, [purchase].[Purchase].[Id] DESC",
        "date_asc" => "ORDER BY [purchase].[Purchase].[InvoiceDate] ASC, [purchase].[Purchase].[Id] ASC",
        "amount_desc" => "ORDER BY [purchase].[Purchase].[TotalAmount] DESC, [purchase].[Purchase].[Id] DESC",
        "amount_asc" => "ORDER BY [purchase].[Purchase].[TotalAmount] ASC, [purchase].[Purchase].[Id] ASC",
        _ => "ORDER BY [purchase].[Purchase].[CreatedAtUtc] DESC, [purchase].[Purchase].[Id] DESC"
    };

    /// <summary>
    /// One page of filtered/sorted purchases plus the total matching count. Filtering, search, sort
    /// and paging all run in SQL, so only the requested page is materialized (the list no longer
    /// loads the entire history into memory). Supplier/ExpenseCategory names for the page are
    /// resolved separately by the service (including inactive ones).
    /// </summary>
    public async Task<(List<Purchase> Items, int TotalCount)> GetPagedAsync(
        int businessId, PurchaseListFilter filter, int page, int pageSize)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            // Count (its own parameter list â€” SqlParameter instances can't be reused across queries).
            var countParams = new List<SqlParameter>();
            var countFromWhere = BuildListFromWhere(businessId, filter, countParams);
            var countSql = "SELECT COUNT(*) AS [Value] " + countFromWhere;
            var total = (await _context.Database.SqlQueryRaw<int>(countSql, countParams.ToArray()).ToListAsync()).FirstOrDefault();

            // Page of entities.
            var pageParams = new List<SqlParameter>();
            var fromWhere = BuildListFromWhere(businessId, filter, pageParams);
            pageParams.Add(new SqlParameter("@Offset", (page - 1) * pageSize));
            pageParams.Add(new SqlParameter("@PageSize", pageSize));

            var pageSql = $@"
                SELECT {PurchaseColumns}
                {fromWhere}
                {BuildOrderBy(filter.SortBy)}
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var items = await _context.Set<Purchase>().FromSqlRaw(pageSql, pageParams.ToArray()).ToListAsync();
            return (items, total);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// All purchases matching the filter (no paging), sorted â€” used by CSV/PDF export so the export
    /// applies the SAME filters and search as the on-screen list, in SQL.
    /// </summary>
    public async Task<List<Purchase>> GetAllFilteredAsync(int businessId, PurchaseListFilter filter)
    {
        try
        {
            var p = new List<SqlParameter>();
            var fromWhere = BuildListFromWhere(businessId, filter, p);
            var sql = $@"
                SELECT {PurchaseColumns}
                {fromWhere}
                {BuildOrderBy(filter.SortBy)}";

            return await _context.Set<Purchase>().FromSqlRaw(sql, p.ToArray()).ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// KPIs + origin counts over the FULL filtered set (not just the current page). Cancelled
    /// purchases are excluded from the money and origin figures. Outdated = invoice date falls
    /// outside the VAT submission period it is assigned to.
    /// </summary>
    public async Task<PurchaseListAggregates> GetAggregatesAsync(int businessId, PurchaseListFilter filter)
    {
        try
        {
            var p = new List<SqlParameter>();
            // "Outdated" compares the invoice date against the assigned VAT period's bounds. SQL
            // Server forbids a subquery inside an aggregate's argument (SUM(CASE ... EXISTS(...)))
            // â€” so the VAT period is LEFT JOINed and the comparison is a plain column expression.
            var fromWhereWithVat = BuildListFromWhere(businessId, filter, p, includeVatJoin: true);

            var sql = $@"
                SELECT
                    SUM(CASE WHEN [purchase].[Purchase].[IsCancelled] = 0 THEN 1 ELSE 0 END) AS [TotalCount],
                    ISNULL(SUM(CASE WHEN [purchase].[Purchase].[IsCancelled] = 0 THEN [purchase].[Purchase].[TotalAmount] ELSE 0 END), 0) AS [TotalSpend],
                    ISNULL(SUM(CASE WHEN [purchase].[Purchase].[IsCancelled] = 0 THEN [purchase].[Purchase].[VatAmount] ELSE 0 END), 0) AS [TotalVat],
                    SUM(CASE WHEN [purchase].[Purchase].[IsCancelled] = 0 AND [purchase].[Purchase].[PurchaseOriginTypeId] = 1 THEN 1 ELSE 0 END) AS [DomesticCount],
                    SUM(CASE WHEN [purchase].[Purchase].[IsCancelled] = 0 AND [purchase].[Purchase].[PurchaseOriginTypeId] = 2 THEN 1 ELSE 0 END) AS [EuRcCount],
                    SUM(CASE WHEN [purchase].[Purchase].[IsCancelled] = 0 AND [purchase].[Purchase].[PurchaseOriginTypeId] = 4 THEN 1 ELSE 0 END) AS [EuPaidCount],
                    SUM(CASE WHEN [purchase].[Purchase].[IsCancelled] = 0 AND [purchase].[Purchase].[PurchaseOriginTypeId] = 3 THEN 1 ELSE 0 END) AS [NonEuCount],
                    SUM(CASE WHEN [purchase].[Purchase].[IsCancelled] = 0
                              AND [vat].[VatSubmissionPeriod].[Id] IS NOT NULL
                              AND ([purchase].[Purchase].[InvoiceDate] < [vat].[VatSubmissionPeriod].[PeriodStartDate]
                                   OR [purchase].[Purchase].[InvoiceDate] > [vat].[VatSubmissionPeriod].[PeriodEndDate])
                         THEN 1 ELSE 0 END) AS [OutdatedCount]
                {fromWhereWithVat}";

            var row = (await _context.Database.SqlQueryRaw<PurchaseListAggregates>(sql, p.ToArray()).ToListAsync()).FirstOrDefault();
            return row ?? new PurchaseListAggregates();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Expense trend: spend grouped by time bucket and by the chosen dimension (expense category or
    /// supplier), over the same filtered set the Purchases list/KPIs use. Reuses the shared
    /// <see cref="BuildListFromWhere"/> predicate and adds the cancelled-exclusion so the summed
    /// result reconciles with the "Total Spend" KPI. Payroll purchases follow the filter's
    /// IncludePayroll rule (already handled by the shared WHERE).
    ///
    /// <paramref name="dimension"/> ("category" | "supplier") and <paramref name="bucket"/>
    /// ("day" | "month" | "year") are server-side whitelisted enums that select fixed SQL
    /// fragments — never interpolated from untrusted input. All filter values flow through
    /// SqlParameter via BuildListFromWhere.
    /// </summary>
    public virtual async Task<List<ExpenseTrendPoint>> GetExpenseTrendAsync(
        int businessId, PurchaseListFilter filter, string dimension, string bucket)
    {
        try
        {
            var p = new List<SqlParameter>();
            var fromWhere = BuildListFromWhere(businessId, filter, p, includeVatJoin: false);

            // Only real spend contributes to the trend (matches the KPI's cancelled-exclusion).
            fromWhere += " AND [purchase].[Purchase].[IsCancelled] = 0";

            // Whitelisted bucket expression on InvoiceDate.
            var bucketExpr = bucket switch
            {
                "day" => "CONVERT(date, [purchase].[Purchase].[InvoiceDate])",
                "year" => "DATEFROMPARTS(YEAR([purchase].[Purchase].[InvoiceDate]), 1, 1)",
                _ => "DATEFROMPARTS(YEAR([purchase].[Purchase].[InvoiceDate]), MONTH([purchase].[Purchase].[InvoiceDate]), 1)", // month (default)
            };

            // Whitelisted dimension: series id + name, and the name-table join to splice in.
            string seriesIdExpr;
            string seriesNameExpr;
            string dimensionJoin;
            if (dimension == "supplier")
            {
                seriesIdExpr = "[purchase].[Purchase].[SupplierId]";
                seriesNameExpr = "[purchase].[Supplier].[Name]";
                dimensionJoin = string.Empty; // Supplier is already LEFT JOINed by BuildListFromWhere.
            }
            else // category (default)
            {
                seriesIdExpr = "[purchase].[Purchase].[ExpenseCategoryId]";
                seriesNameExpr = "[purchase].[ExpenseCategory].[Name]";
                dimensionJoin =
                    "\n                 LEFT JOIN [purchase].[ExpenseCategory] ON [purchase].[ExpenseCategory].[Id] = [purchase].[Purchase].[ExpenseCategoryId]";
            }

            // Splice the dimension join in right before the WHERE of the shared FROM/WHERE.
            // Whitespace-independent: insert before the first "WHERE " token.
            var whereIdx = fromWhere.IndexOf("WHERE ", StringComparison.Ordinal);
            var fromWhereWithJoin = whereIdx < 0
                ? fromWhere + dimensionJoin
                : fromWhere.Insert(whereIdx, dimensionJoin + "\n                 ");

            var sql = $@"
                SELECT {bucketExpr} AS [Bucket],
                       {seriesIdExpr} AS [SeriesId],
                       {seriesNameExpr} AS [SeriesName],
                       ISNULL(SUM([purchase].[Purchase].[TotalAmount]), 0) AS [Amount]
                {fromWhereWithJoin}
                GROUP BY {bucketExpr}, {seriesIdExpr}, {seriesNameExpr}
                ORDER BY [Bucket]";

            return await _context.Database
                .SqlQueryRaw<ExpenseTrendPoint>(sql, p.ToArray())
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Name lookups for a set of supplier ids (including inactive suppliers), so the list can show
    /// the supplier name on every row â€” the active-only service load left inactive ones blank.
    /// </summary>
    public async Task<Dictionary<int, string>> GetSupplierNamesAsync(int businessId, IEnumerable<int> supplierIds)
    {
        try
        {
            var ids = supplierIds.Where(i => i > 0).Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, string>();

            var inList = string.Join(",", ids);
            var sql = $@"
                SELECT [purchase].[Supplier].[Id] AS [Key], [purchase].[Supplier].[Name] AS [Value]
                FROM [purchase].[Supplier]
                WHERE [purchase].[Supplier].[BusinessId] = @BusinessId
                  AND [purchase].[Supplier].[Id] IN ({inList})";

            var rows = await _context.Database
                .SqlQueryRaw<IdNameRow>(sql, new SqlParameter("@BusinessId", businessId))
                .ToListAsync();
            return rows.ToDictionary(r => r.Key, r => r.Value);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Name lookups for a set of expense-category ids (including inactive categories).</summary>
    public async Task<Dictionary<int, string>> GetExpenseCategoryNamesAsync(int businessId, IEnumerable<int> categoryIds)
    {
        try
        {
            var ids = categoryIds.Where(i => i > 0).Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, string>();

            var inList = string.Join(",", ids);
            var sql = $@"
                SELECT [purchase].[ExpenseCategory].[Id] AS [Key], [purchase].[ExpenseCategory].[Name] AS [Value]
                FROM [purchase].[ExpenseCategory]
                WHERE [purchase].[ExpenseCategory].[BusinessId] = @BusinessId
                  AND [purchase].[ExpenseCategory].[Id] IN ({inList})";

            var rows = await _context.Database
                .SqlQueryRaw<IdNameRow>(sql, new SqlParameter("@BusinessId", businessId))
                .ToListAsync();
            return rows.ToDictionary(r => r.Key, r => r.Value);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // Row shape for idâ†’name keyless projections above.
    private class IdNameRow
    {
        public int Key { get; set; }
        public string Value { get; set; } = string.Empty;
    }
}
