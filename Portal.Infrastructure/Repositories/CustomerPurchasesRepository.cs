using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Read-only repository for the Customer Purchases Dashboard. All dashboard data is read in ONE batched
/// command (five result sets) to keep database round trips to a minimum. Every statement is scoped by
/// @BusinessId, uses full table names and never references a quotation table. The only derived-table
/// name is [PaymentTotals], which T-SQL requires for OUTER APPLY.
/// </summary>
public class CustomerPurchasesRepository : GenericStoredProcedureRepository<InvoiceFinancialRow>
{
    /// <summary>Invoice status Draft.</summary>
    private const int StatusDraft = 1;

    /// <summary>Invoice status Cancelled.</summary>
    private const int StatusCancelled = 3;

    /// <summary>Credit note status Issued.</summary>
    private const int StatusIssued = 2;

    /// <summary>Credit note status Applied.</summary>
    private const int StatusApplied = 3;

    /// <summary>1. Every counted invoice of the customer (all currencies, all time). Valid payments are summed once per invoice.</summary>
    internal const string InvoiceFinancialsSql = @"
        SELECT [invoice].[Invoice].[Id],
               [invoice].[Invoice].[InvoiceNumber],
               [invoice].[Invoice].[InvoiceDate],
               [invoice].[Invoice].[DueDate],
               [invoice].[Invoice].[CurrencyCode],
               [invoice].[Invoice].[TotalAmount],
               ISNULL([PaymentTotals].[TotalPaid], 0) AS [TotalPaid],
               [PaymentTotals].[LastPaymentDateUtc],
               (SELECT ISNULL(SUM([credit].[CreditNoteApplication].[AmountApplied]), 0)
                FROM [credit].[CreditNoteApplication]
                INNER JOIN [credit].[CreditNote]
                    ON [credit].[CreditNoteApplication].[CreditNoteId] = [credit].[CreditNote].[Id]
                WHERE [credit].[CreditNoteApplication].[InvoiceId] = [invoice].[Invoice].[Id]
                  AND [credit].[CreditNote].[BusinessId] = @BusinessId
                  AND [credit].[CreditNoteApplication].[IsVoided] = 0) AS [TotalCredited],
               CASE WHEN [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = 5 THEN 1 ELSE 0 END AS [IsWrittenOff]
        FROM [invoice].[Invoice]
        OUTER APPLY (
            SELECT SUM([revenue].[Payment].[Amount]) AS [TotalPaid],
                   MAX([revenue].[Payment].[PaymentDateUtc]) AS [LastPaymentDateUtc]
            FROM [revenue].[Payment]
            WHERE [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
              AND [revenue].[Payment].[BusinessId] = @BusinessId
              AND [revenue].[Payment].[IsVoided] = 0
              AND [revenue].[Payment].[PaymentDateUtc] <= GETUTCDATE()) AS [PaymentTotals]
        WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
          AND [invoice].[Invoice].[CustomerId] = @CustomerId
          AND [invoice].[Invoice].[IsDeleted] = 0
          AND [invoice].[Invoice].[InvoiceStatusTypeId] <> @StatusDraft
          AND [invoice].[Invoice].[InvoiceStatusTypeId] <> @StatusCancelled
        ORDER BY [invoice].[Invoice].[InvoiceDate], [invoice].[Invoice].[Id];
";

    /// <summary>2. Non-adjustment product lines of counted invoices, all currencies, all time.</summary>
    internal const string ProductLinesSql = @"
        SELECT [invoice].[InvoiceLine].[InvoiceId],
               [invoice].[InvoiceLine].[Id],
               [invoice].[Invoice].[InvoiceDate],
               [invoice].[InvoiceLine].[ProductCode],
               [invoice].[InvoiceLine].[Description],
               [invoice].[InvoiceLine].[Quantity],
               [invoice].[InvoiceLine].[LineTotal],
               [invoice].[InvoiceLine].[CostPrice]
        FROM [invoice].[InvoiceLine]
        INNER JOIN [invoice].[Invoice]
            ON [invoice].[InvoiceLine].[InvoiceId] = [invoice].[Invoice].[Id]
        WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
          AND [invoice].[Invoice].[CustomerId] = @CustomerId
          AND [invoice].[Invoice].[IsDeleted] = 0
          AND [invoice].[Invoice].[InvoiceStatusTypeId] <> @StatusDraft
          AND [invoice].[Invoice].[InvoiceStatusTypeId] <> @StatusCancelled
          AND [invoice].[InvoiceLine].[IsAdjustmentLine] = 0;
";

    /// <summary>3. Count and total of Issued/Applied credit notes in the period, per currency of the linked invoice.</summary>
    internal const string CreditNoteSummarySql = @"
        SELECT [invoice].[Invoice].[CurrencyCode],
               COUNT(*) AS [CreditNoteCount],
               ISNULL(SUM([credit].[CreditNote].[TotalAmount]), 0) AS [CreditNoteTotal]
        FROM [credit].[CreditNote]
        INNER JOIN [invoice].[Invoice]
            ON [credit].[CreditNote].[InvoiceId] = [invoice].[Invoice].[Id]
        WHERE [credit].[CreditNote].[BusinessId] = @BusinessId
          AND [credit].[CreditNote].[CustomerId] = @CustomerId
          AND [credit].[CreditNote].[CreditNoteStatusTypeId] IN (@StatusIssued, @StatusApplied)
          AND [invoice].[Invoice].[BusinessId] = @BusinessId
          AND (@From IS NULL OR [credit].[CreditNote].[IssueDate] >= @From)
          AND (@To IS NULL OR [credit].[CreditNote].[IssueDate] <= @To)
        GROUP BY [invoice].[Invoice].[CurrencyCode];
";

    /// <summary>4. Active catalogue list prices for the business.</summary>
    internal const string CatalogPricesSql = @"
        SELECT [product].[Product].[ProductCode], [product].[Product].[DefaultSellingPrice]
        FROM [product].[Product]
        WHERE [product].[Product].[BusinessId] = @BusinessId
          AND [product].[Product].[IsActive] = 1;
";

    /// <summary>5. Most-used invoice currency of the business (ties broken alphabetically).</summary>
    internal const string BusinessPrimaryCurrencySql = @"
        SELECT TOP (1) [invoice].[Invoice].[CurrencyCode]
        FROM [invoice].[Invoice]
        WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
          AND [invoice].[Invoice].[IsDeleted] = 0
          AND [invoice].[Invoice].[InvoiceStatusTypeId] <> @StatusDraft
          AND [invoice].[Invoice].[InvoiceStatusTypeId] <> @StatusCancelled
        GROUP BY [invoice].[Invoice].[CurrencyCode]
        ORDER BY COUNT(*) DESC, [invoice].[Invoice].[CurrencyCode] ASC;
";

    /// <summary>The five statements above, sent as one command.</summary>
    internal const string DashboardBatchSql =
        InvoiceFinancialsSql + ProductLinesSql + CreditNoteSummarySql + CatalogPricesSql + BusinessPrimaryCurrencySql;

    public CustomerPurchasesRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Reads every dashboard input in one round trip. <paramref name="creditFrom"/> and <paramref name="creditTo"/>
    /// bound the credit note summary (null means unbounded).
    /// </summary>
    public virtual async Task<PurchasesDataSet> GetDashboardDataAsync(int businessId, int customerId, DateOnly? creditFrom, DateOnly? creditTo)
    {
        try
        {
            var invoices = new List<InvoiceFinancialRow>();
            var lines = new List<ProductLineRow>();
            var credit = new Dictionary<string, CreditNoteSummaryRow>(StringComparer.Ordinal);
            var catalogue = new List<CatalogPriceRow>();
            string? primaryCurrency = null;

            var connection = _context.Database.GetDbConnection();
            try
            {
                await OpenIfNeededAsync(connection);

                using var command = CreateCommand(connection, DashboardBatchSql);
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@CustomerId", customerId));
                command.Parameters.Add(new SqlParameter("@StatusDraft", StatusDraft));
                command.Parameters.Add(new SqlParameter("@StatusCancelled", StatusCancelled));
                command.Parameters.Add(new SqlParameter("@StatusIssued", StatusIssued));
                command.Parameters.Add(new SqlParameter("@StatusApplied", StatusApplied));
                command.Parameters.Add(new SqlParameter("@From", SqlDbType.Date)
                {
                    Value = creditFrom.HasValue ? creditFrom.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value
                });
                command.Parameters.Add(new SqlParameter("@To", SqlDbType.Date)
                {
                    Value = creditTo.HasValue ? creditTo.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value
                });

                using var reader = await command.ExecuteReaderAsync();

                // 1. Invoices
                while (await reader.ReadAsync())
                {
                    int lastPaymentOrdinal = reader.GetOrdinal("LastPaymentDateUtc");
                    invoices.Add(new InvoiceFinancialRow(
                        reader.GetInt32(reader.GetOrdinal("Id")),
                        reader.GetString(reader.GetOrdinal("InvoiceNumber")),
                        DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("InvoiceDate"))),
                        DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("DueDate"))),
                        reader.GetString(reader.GetOrdinal("CurrencyCode")),
                        reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                        reader.GetDecimal(reader.GetOrdinal("TotalPaid")),
                        reader.IsDBNull(lastPaymentOrdinal) ? null : reader.GetDateTime(lastPaymentOrdinal),
                        reader.GetDecimal(reader.GetOrdinal("TotalCredited")),
                        reader.GetInt32(reader.GetOrdinal("IsWrittenOff")) == 1));
                }

                // 2. Product lines
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    int productCodeOrdinal = reader.GetOrdinal("ProductCode");
                    int costOrdinal = reader.GetOrdinal("CostPrice");
                    lines.Add(new ProductLineRow(
                        reader.GetInt32(reader.GetOrdinal("InvoiceId")),
                        reader.GetInt32(reader.GetOrdinal("Id")),
                        DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("InvoiceDate"))),
                        reader.IsDBNull(productCodeOrdinal) ? null : reader.GetString(productCodeOrdinal),
                        reader.GetString(reader.GetOrdinal("Description")),
                        reader.GetDecimal(reader.GetOrdinal("Quantity")),
                        reader.GetDecimal(reader.GetOrdinal("LineTotal")),
                        reader.IsDBNull(costOrdinal) ? null : reader.GetDecimal(costOrdinal)));
                }

                // 3. Credit note summary per currency
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    credit[reader.GetString(reader.GetOrdinal("CurrencyCode"))] = new CreditNoteSummaryRow(
                        reader.GetInt32(reader.GetOrdinal("CreditNoteCount")),
                        reader.GetDecimal(reader.GetOrdinal("CreditNoteTotal")));
                }

                // 4. Catalogue prices
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    int codeOrdinal = reader.GetOrdinal("ProductCode");
                    int priceOrdinal = reader.GetOrdinal("DefaultSellingPrice");
                    if (reader.IsDBNull(codeOrdinal) || reader.IsDBNull(priceOrdinal))
                        continue;
                    catalogue.Add(new CatalogPriceRow(reader.GetString(codeOrdinal), reader.GetDecimal(priceOrdinal)));
                }

                // 5. Business primary currency
                await reader.NextResultAsync();
                if (await reader.ReadAsync() && !reader.IsDBNull(0))
                {
                    primaryCurrency = reader.GetString(0);
                }
            }
            finally
            {
                await CloseIfOwnedAsync(connection);
            }

            return new PurchasesDataSet(invoices, lines, credit, catalogue, primaryCurrency);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static async Task OpenIfNeededAsync(DbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();
    }

    private DbCommand CreateCommand(DbConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;

        var transaction = _context.Database.CurrentTransaction;
        if (transaction != null)
            command.Transaction = transaction.GetDbTransaction();

        return command;
    }

    private async Task CloseIfOwnedAsync(DbConnection connection)
    {
        if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
            await connection.CloseAsync();
    }
}