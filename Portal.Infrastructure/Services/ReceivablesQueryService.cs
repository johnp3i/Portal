using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides filtered, paginated lists of issued invoices with their financial state.
/// Queries non-deleted invoices with InvoiceStatusTypeId = 2 (Issued) for a given business,
/// joining Customer for name and computing TotalPaid via subquery on valid payments.
/// </summary>
public class ReceivablesQueryService : IReceivablesQueryService
{
    private readonly PortalDbContext _context;

    public ReceivablesQueryService(PortalDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ReceivableDto>> GetReceivablesAsync(
        int businessId,
        string? searchTerm = null,
        int? financialStatusFilter = null,
        int? customerFilter = null,
        DateOnly? dueFrom = null,
        DateOnly? dueTo = null,
        int page = 1,
        int pageSize = 15)
    {
        try
        {
            // Clamp page to minimum 1
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 15;

            int offset = (page - 1) * pageSize;

            // Escape SQL wildcards in search term
            string? escapedSearchTerm = searchTerm != null
                ? searchTerm.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")
                : null;

            // Build dynamic WHERE clause for optional filters
            var whereClauses = new List<string>
            {
                "[invoice].[Invoice].[BusinessId] = @BusinessId",
                "[invoice].[Invoice].[IsDeleted] = 0",
                "[invoice].[Invoice].[InvoiceStatusTypeId] = 2"
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                whereClauses.Add(@"([invoice].[Invoice].[InvoiceNumber] LIKE '%' + @SearchTerm + '%'
                       OR [customer].[Customer].[Name] LIKE '%' + @SearchTerm + '%')");
            }

            if (financialStatusFilter.HasValue)
            {
                whereClauses.Add("[invoice].[Invoice].[InvoiceFinancialStatusTypeId] = @FinancialStatusFilter");
            }

            if (customerFilter.HasValue)
            {
                whereClauses.Add("[invoice].[Invoice].[CustomerId] = @CustomerFilter");
            }

            if (dueFrom.HasValue)
            {
                whereClauses.Add("[invoice].[Invoice].[DueDate] >= @DueFrom");
            }

            if (dueTo.HasValue)
            {
                whereClauses.Add("[invoice].[Invoice].[DueDate] <= @DueTo");
            }

            var whereClause = string.Join(" AND ", whereClauses);

            var countQuery = $@"
                SELECT COUNT(*)
                FROM [invoice].[Invoice]
                INNER JOIN [customer].[Customer]
                    ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [invoice].[InvoiceFinancialStatusType]
                    ON [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = [invoice].[InvoiceFinancialStatusType].[Id]
                WHERE {whereClause}";

            var dataQuery = $@"
                SELECT [invoice].[Invoice].[Id],
                       [invoice].[Invoice].[InvoiceNumber],
                       [customer].[Customer].[Name] AS [CustomerName],
                       [invoice].[Invoice].[InvoiceDate],
                       [invoice].[Invoice].[DueDate],
                       [invoice].[Invoice].[TotalAmount],
                       (SELECT ISNULL(SUM([revenue].[Payment].[Amount]), 0)
                        FROM [revenue].[Payment]
                        WHERE [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
                          AND [revenue].[Payment].[IsVoided] = 0) AS [TotalPaid],
                       [invoice].[Invoice].[TotalAmount] -
                       (SELECT ISNULL(SUM([revenue].[Payment].[Amount]), 0)
                        FROM [revenue].[Payment]
                        WHERE [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
                          AND [revenue].[Payment].[IsVoided] = 0) AS [OutstandingBalance],
                       [invoice].[Invoice].[InvoiceFinancialStatusTypeId],
                       [invoice].[InvoiceFinancialStatusType].[Name] AS [FinancialStatusName]
                FROM [invoice].[Invoice]
                INNER JOIN [customer].[Customer]
                    ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [invoice].[InvoiceFinancialStatusType]
                    ON [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = [invoice].[InvoiceFinancialStatusType].[Id]
                WHERE {whereClause}
                ORDER BY [invoice].[Invoice].[DueDate] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _context.Database.CurrentTransaction;

                // Execute count query
                int totalCount;
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;

                    if (transaction != null)
                        countCommand.Transaction = transaction.GetDbTransaction();

                    AddParameters(countCommand, businessId, escapedSearchTerm,
                        financialStatusFilter, customerFilter, dueFrom, dueTo);

                    var countResult = await countCommand.ExecuteScalarAsync();
                    totalCount = (int)countResult!;
                }

                // Execute data query
                var items = new List<ReceivableDto>();
                using (var dataCommand = connection.CreateCommand())
                {
                    dataCommand.CommandText = dataQuery;

                    if (transaction != null)
                        dataCommand.Transaction = transaction.GetDbTransaction();

                    AddParameters(dataCommand, businessId, escapedSearchTerm,
                        financialStatusFilter, customerFilter, dueFrom, dueTo);
                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var outstandingBalance = reader.GetDecimal(reader.GetOrdinal("OutstandingBalance"));

                        items.Add(new ReceivableDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            InvoiceNumber = reader.GetString(reader.GetOrdinal("InvoiceNumber")),
                            CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                            InvoiceDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("InvoiceDate"))),
                            DueDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("DueDate"))),
                            TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                            TotalPaid = reader.GetDecimal(reader.GetOrdinal("TotalPaid")),
                            OutstandingBalance = outstandingBalance,
                            InvoiceFinancialStatusTypeId = reader.GetInt32(reader.GetOrdinal("InvoiceFinancialStatusTypeId")),
                            FinancialStatusName = reader.GetString(reader.GetOrdinal("FinancialStatusName")),
                            HasOutstandingBalance = outstandingBalance > 0
                        });
                    }
                }

                return new PagedResult<ReceivableDto>
                {
                    Items = items,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
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

    private static void AddParameters(
        System.Data.Common.DbCommand command,
        int businessId,
        string? escapedSearchTerm,
        int? financialStatusFilter,
        int? customerFilter,
        DateOnly? dueFrom,
        DateOnly? dueTo)
    {
        command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

        if (!string.IsNullOrWhiteSpace(escapedSearchTerm))
            command.Parameters.Add(new SqlParameter("@SearchTerm", escapedSearchTerm));

        if (financialStatusFilter.HasValue)
            command.Parameters.Add(new SqlParameter("@FinancialStatusFilter", financialStatusFilter.Value));

        if (customerFilter.HasValue)
            command.Parameters.Add(new SqlParameter("@CustomerFilter", customerFilter.Value));

        if (dueFrom.HasValue)
            command.Parameters.Add(new SqlParameter("@DueFrom", dueFrom.Value.ToDateTime(TimeOnly.MinValue)));

        if (dueTo.HasValue)
            command.Parameters.Add(new SqlParameter("@DueTo", dueTo.Value.ToDateTime(TimeOnly.MinValue)));
    }
}
