using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Computes KPI aggregates, chart data, and summary tables for the revenue dashboard.
/// Uses raw SQL queries via the PaymentRepository and direct DbConnection for complex aggregations.
/// All queries are scoped to businessId for tenant isolation.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly PortalDbContext _dbContext;
    private readonly PaymentRepository _paymentRepository;

    // Financial status constants
    private const int FinancialStatusUnpaid = 1;
    private const int FinancialStatusPartiallyPaid = 2;
    private const int FinancialStatusPaid = 3;
    private const int FinancialStatusOverdue = 4;

    // Invoice status constants
    private const int InvoiceStatusIssued = 2;

    public DashboardService(
        PortalDbContext dbContext,
        PaymentRepository paymentRepository)
    {
        _dbContext = dbContext;
        _paymentRepository = paymentRepository;
    }

    /// <inheritdoc />
    public async Task<DashboardKpiDto> GetKpiDataAsync(int businessId)
    {
        try
        {
            var result = new DashboardKpiDto();

            var connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _dbContext.Database.CurrentTransaction;

                // Outstanding Receivables: sum of OutstandingBalance across non-deleted invoices
                // with InvoiceStatusTypeId = 2 (Issued) AND InvoiceFinancialStatusTypeId in (1, 2, 4)
                const string outstandingQuery = @"
                    SELECT ISNULL(SUM([invoice].[Invoice].[TotalAmount] - ISNULL(ValidPayments.TotalPaid, 0)), 0) AS [OutstandingReceivables],
                           COUNT(*) AS [OutstandingInvoiceCount]
                    FROM [invoice].[Invoice]
                    LEFT JOIN (
                        SELECT [revenue].[Payment].[InvoiceId],
                               SUM([revenue].[Payment].[Amount]) AS [TotalPaid]
                        FROM [revenue].[Payment]
                        WHERE [revenue].[Payment].[IsVoided] = 0
                          AND [revenue].[Payment].[BusinessId] = @BusinessId
                          AND [revenue].[Payment].[PaymentDateUtc] <= GETUTCDATE()
                        GROUP BY [revenue].[Payment].[InvoiceId]
                    ) AS ValidPayments ON [invoice].[Invoice].[Id] = ValidPayments.[InvoiceId]
                    WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                      AND [invoice].[Invoice].[IsDeleted] = 0
                      AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                      AND [invoice].[Invoice].[InvoiceFinancialStatusTypeId] IN (@StatusUnpaid, @StatusPartiallyPaid, @StatusOverdue)";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = outstandingQuery;
                    if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                    command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    command.Parameters.Add(new SqlParameter("@InvoiceStatusIssued", InvoiceStatusIssued));
                    command.Parameters.Add(new SqlParameter("@StatusUnpaid", FinancialStatusUnpaid));
                    command.Parameters.Add(new SqlParameter("@StatusPartiallyPaid", FinancialStatusPartiallyPaid));
                    command.Parameters.Add(new SqlParameter("@StatusOverdue", FinancialStatusOverdue));

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        result.OutstandingReceivables = reader.GetDecimal(0);
                        result.OutstandingInvoiceCount = reader.GetInt32(1);
                    }
                }

                // Overdue Amount: sum of OutstandingBalance where DueDate < today AND OutstandingBalance > 0
                const string overdueQuery = @"
                    SELECT ISNULL(SUM([invoice].[Invoice].[TotalAmount] - ISNULL(ValidPayments.TotalPaid, 0)), 0) AS [OverdueAmount],
                           COUNT(*) AS [OverdueInvoiceCount]
                    FROM [invoice].[Invoice]
                    LEFT JOIN (
                        SELECT [revenue].[Payment].[InvoiceId],
                               SUM([revenue].[Payment].[Amount]) AS [TotalPaid]
                        FROM [revenue].[Payment]
                        WHERE [revenue].[Payment].[IsVoided] = 0
                          AND [revenue].[Payment].[BusinessId] = @BusinessId
                          AND [revenue].[Payment].[PaymentDateUtc] <= GETUTCDATE()
                        GROUP BY [revenue].[Payment].[InvoiceId]
                    ) AS ValidPayments ON [invoice].[Invoice].[Id] = ValidPayments.[InvoiceId]
                    WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                      AND [invoice].[Invoice].[IsDeleted] = 0
                      AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                      AND [invoice].[Invoice].[DueDate] < @Today
                      AND ([invoice].[Invoice].[TotalAmount] - ISNULL(ValidPayments.TotalPaid, 0)) > 0";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = overdueQuery;
                    if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                    command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    command.Parameters.Add(new SqlParameter("@InvoiceStatusIssued", InvoiceStatusIssued));
                    command.Parameters.Add(new SqlParameter("@Today", DateOnly.FromDateTime(DateTime.UtcNow)));

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        result.OverdueAmount = reader.GetDecimal(0);
                        result.OverdueInvoiceCount = reader.GetInt32(1);
                    }
                }

                // Paid This Month: sum of Payment.Amount where IsVoided = 0 and PaymentDateUtc in current calendar month
                var now = DateTime.UtcNow;
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1);

                const string paidThisMonthQuery = @"
                    SELECT ISNULL(SUM([revenue].[Payment].[Amount]), 0) AS [PaidThisMonth],
                           COUNT(*) AS [PaidThisMonthCount]
                    FROM [revenue].[Payment]
                    WHERE [revenue].[Payment].[BusinessId] = @BusinessId
                      AND [revenue].[Payment].[IsVoided] = 0
                      AND [revenue].[Payment].[ParentPaymentId] IS NULL
                      AND [revenue].[Payment].[PaymentDateUtc] >= @MonthStart
                      AND [revenue].[Payment].[PaymentDateUtc] < @MonthEnd
                      AND [revenue].[Payment].[PaymentDateUtc] <= GETUTCDATE()";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = paidThisMonthQuery;
                    if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                    command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    command.Parameters.Add(new SqlParameter("@MonthStart", monthStart));
                    command.Parameters.Add(new SqlParameter("@MonthEnd", monthEnd));

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        result.PaidThisMonth = reader.GetDecimal(0);
                        result.PaidThisMonthCount = reader.GetInt32(1);
                    }
                }

                // Partially Paid: sum of OutstandingBalance where InvoiceFinancialStatusTypeId = 2
                const string partiallyPaidQuery = @"
                    SELECT ISNULL(SUM([invoice].[Invoice].[TotalAmount] - ISNULL(ValidPayments.TotalPaid, 0)), 0) AS [PartiallyPaidAmount],
                           COUNT(*) AS [PartiallyPaidCount]
                    FROM [invoice].[Invoice]
                    LEFT JOIN (
                        SELECT [revenue].[Payment].[InvoiceId],
                               SUM([revenue].[Payment].[Amount]) AS [TotalPaid]
                        FROM [revenue].[Payment]
                        WHERE [revenue].[Payment].[IsVoided] = 0
                          AND [revenue].[Payment].[BusinessId] = @BusinessId
                          AND [revenue].[Payment].[PaymentDateUtc] <= GETUTCDATE()
                        GROUP BY [revenue].[Payment].[InvoiceId]
                    ) AS ValidPayments ON [invoice].[Invoice].[Id] = ValidPayments.[InvoiceId]
                    WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                      AND [invoice].[Invoice].[IsDeleted] = 0
                      AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                      AND [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = @StatusPartiallyPaid";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = partiallyPaidQuery;
                    if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                    command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    command.Parameters.Add(new SqlParameter("@InvoiceStatusIssued", InvoiceStatusIssued));
                    command.Parameters.Add(new SqlParameter("@StatusPartiallyPaid", FinancialStatusPartiallyPaid));

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        result.PartiallyPaidAmount = reader.GetDecimal(0);
                        result.PartiallyPaidCount = reader.GetInt32(1);
                    }
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<MonthlyRevenueDto>> GetRevenueCollectedAsync(int businessId)
    {
        try
        {
            var fromUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(-11);

            var monthlyPayments = await _paymentRepository.GetMonthlyTotalsAsync(businessId, fromUtc);

            // Add Z-Report revenue if feature is enabled
            var profile = await _dbContext.BusinessProfiles
                .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);

            if (profile?.IsZReportEnabled == true)
            {
                var fromDate = DateOnly.FromDateTime(fromUtc);

                var zReportMonthly = await _dbContext.RevenueSummaries
                    .Where(rs => rs.BusinessId == businessId
                        && rs.IsActive
                        && rs.SummaryDate >= fromDate)
                    .GroupBy(rs => new { rs.SummaryDate.Year, rs.SummaryDate.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(rs => rs.TotalGross) })
                    .ToListAsync();

                foreach (var zMonth in zReportMonthly)
                {
                    var existing = monthlyPayments.FirstOrDefault(m => m.Year == zMonth.Year && m.Month == zMonth.Month);
                    if (existing != null)
                    {
                        existing.Amount += zMonth.Total;
                        existing.IncludesPosRevenue = true;
                    }
                    else
                    {
                        monthlyPayments.Add(new MonthlyRevenueDto
                        {
                            Year = zMonth.Year,
                            Month = zMonth.Month,
                            Label = new DateTime(zMonth.Year, zMonth.Month, 1).ToString("MMM"),
                            Amount = zMonth.Total,
                            IncludesPosRevenue = true
                        });
                    }
                }

                monthlyPayments = monthlyPayments.OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();
            }

            return monthlyPayments;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<InvoicedVsCollectedDto>> GetInvoicedVsCollectedAsync(int businessId)
    {
        try
        {
            var now = DateTime.UtcNow;
            var fromDate = new DateOnly(now.Year, now.Month, 1).AddMonths(-11);

            var connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _dbContext.Database.CurrentTransaction;

                // Get monthly invoiced totals (by InvoiceDate) for last 12 months
                const string query = @"
                    SELECT Months.[Year], Months.[Month],
                           ISNULL(InvoicedData.[InvoicedAmount], 0) AS [InvoicedAmount],
                           ISNULL(CollectedData.[CollectedAmount], 0) AS [CollectedAmount]
                    FROM (
                        SELECT YEAR([invoice].[Invoice].[InvoiceDate]) AS [Year],
                               MONTH([invoice].[Invoice].[InvoiceDate]) AS [Month]
                        FROM [invoice].[Invoice]
                        WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                          AND [invoice].[Invoice].[IsDeleted] = 0
                          AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                          AND [invoice].[Invoice].[InvoiceDate] >= @FromDate
                        UNION
                        SELECT YEAR([revenue].[Payment].[PaymentDateUtc]) AS [Year],
                               MONTH([revenue].[Payment].[PaymentDateUtc]) AS [Month]
                        FROM [revenue].[Payment]
                        WHERE [revenue].[Payment].[BusinessId] = @BusinessId
                          AND [revenue].[Payment].[IsVoided] = 0
                          AND [revenue].[Payment].[ParentPaymentId] IS NULL
                          AND [revenue].[Payment].[PaymentDateUtc] >= @FromDateUtc
                    ) AS Months
                    LEFT JOIN (
                        SELECT YEAR([invoice].[Invoice].[InvoiceDate]) AS [Year],
                               MONTH([invoice].[Invoice].[InvoiceDate]) AS [Month],
                               SUM([invoice].[Invoice].[TotalAmount]) AS [InvoicedAmount]
                        FROM [invoice].[Invoice]
                        WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                          AND [invoice].[Invoice].[IsDeleted] = 0
                          AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                          AND [invoice].[Invoice].[InvoiceDate] >= @FromDate
                        GROUP BY YEAR([invoice].[Invoice].[InvoiceDate]), MONTH([invoice].[Invoice].[InvoiceDate])
                    ) AS InvoicedData ON Months.[Year] = InvoicedData.[Year] AND Months.[Month] = InvoicedData.[Month]
                    LEFT JOIN (
                        SELECT YEAR([revenue].[Payment].[PaymentDateUtc]) AS [Year],
                               MONTH([revenue].[Payment].[PaymentDateUtc]) AS [Month],
                               SUM([revenue].[Payment].[Amount]) AS [CollectedAmount]
                        FROM [revenue].[Payment]
                        WHERE [revenue].[Payment].[BusinessId] = @BusinessId
                          AND [revenue].[Payment].[IsVoided] = 0
                          AND [revenue].[Payment].[ParentPaymentId] IS NULL
                          AND [revenue].[Payment].[PaymentDateUtc] >= @FromDateUtc
                        GROUP BY YEAR([revenue].[Payment].[PaymentDateUtc]), MONTH([revenue].[Payment].[PaymentDateUtc])
                    ) AS CollectedData ON Months.[Year] = CollectedData.[Year] AND Months.[Month] = CollectedData.[Month]
                    ORDER BY Months.[Year], Months.[Month]";

                var results = new List<InvoicedVsCollectedDto>();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@InvoiceStatusIssued", InvoiceStatusIssued));
                command.Parameters.Add(new SqlParameter("@FromDate", fromDate));
                command.Parameters.Add(new SqlParameter("@FromDateUtc", fromDate.ToDateTime(TimeOnly.MinValue)));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var year = reader.GetInt32(0);
                    var month = reader.GetInt32(1);
                    results.Add(new InvoicedVsCollectedDto
                    {
                        Year = year,
                        Month = month,
                        Label = new DateTime(year, month, 1).ToString("MMM"),
                        InvoicedAmount = reader.GetDecimal(2),
                        CollectedAmount = reader.GetDecimal(3)
                    });
                }

                // Add Z-Report revenue to InvoicedAmount if feature is enabled
                var profile = await _dbContext.BusinessProfiles
                    .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);

                if (profile?.IsZReportEnabled == true)
                {
                    var zReportMonthly = await _dbContext.RevenueSummaries
                        .Where(rs => rs.BusinessId == businessId
                            && rs.IsActive
                            && rs.SummaryDate >= fromDate)
                        .GroupBy(rs => new { rs.SummaryDate.Year, rs.SummaryDate.Month })
                        .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(rs => rs.TotalGross) })
                        .ToListAsync();

                    foreach (var zMonth in zReportMonthly)
                    {
                        var existing = results.FirstOrDefault(r => r.Year == zMonth.Year && r.Month == zMonth.Month);
                        if (existing != null)
                        {
                            existing.InvoicedAmount += zMonth.Total;
                        }
                        else
                        {
                            results.Add(new InvoicedVsCollectedDto
                            {
                                Year = zMonth.Year,
                                Month = zMonth.Month,
                                Label = new DateTime(zMonth.Year, zMonth.Month, 1).ToString("MMM"),
                                InvoicedAmount = zMonth.Total,
                                CollectedAmount = 0
                            });
                        }
                    }

                    results = results.OrderBy(r => r.Year).ThenBy(r => r.Month).ToList();
                }

                return results;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<decimal> GetCollectionRateAsync(int businessId)
    {
        try
        {
            var now = DateTime.UtcNow;
            var fromDate = new DateOnly(now.Year, now.Month, 1).AddMonths(-11);

            var connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _dbContext.Database.CurrentTransaction;

                // Collection Rate: percentage of total invoiced amount that has been collected
                // within 30 days of invoice date, for invoices issued in the last 12 months
                const string query = @"
                    SELECT
                        CASE
                            WHEN ISNULL(SUM([invoice].[Invoice].[TotalAmount]), 0) = 0 THEN 0
                            ELSE (ISNULL(SUM(CollectedWithin30.PaidAmount), 0) * 100.0) / SUM([invoice].[Invoice].[TotalAmount])
                        END AS [CollectionRate]
                    FROM [invoice].[Invoice]
                    LEFT JOIN (
                        SELECT [revenue].[Payment].[InvoiceId],
                               SUM([revenue].[Payment].[Amount]) AS [PaidAmount]
                        FROM [revenue].[Payment]
                        INNER JOIN [invoice].[Invoice] AS Inv
                            ON [revenue].[Payment].[InvoiceId] = Inv.[Id]
                        WHERE [revenue].[Payment].[IsVoided] = 0
                          AND [revenue].[Payment].[ParentPaymentId] IS NULL
                          AND [revenue].[Payment].[BusinessId] = @BusinessId
                          AND DATEDIFF(DAY, Inv.[InvoiceDate], [revenue].[Payment].[PaymentDateUtc]) <= 30
                        GROUP BY [revenue].[Payment].[InvoiceId]
                    ) AS CollectedWithin30 ON [invoice].[Invoice].[Id] = CollectedWithin30.[InvoiceId]
                    WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                      AND [invoice].[Invoice].[IsDeleted] = 0
                      AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                      AND [invoice].[Invoice].[InvoiceDate] >= @FromDate";

                using var command = connection.CreateCommand();
                command.CommandText = query;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@InvoiceStatusIssued", InvoiceStatusIssued));
                command.Parameters.Add(new SqlParameter("@FromDate", fromDate));

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? Math.Round((decimal)result, 2) : 0m;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PagedResult<OverdueInvoiceDto>> GetOverdueInvoicesAsync(
        int businessId, string? searchTerm, int page, int pageSize)
    {
        try
        {
            var connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _dbContext.Database.CurrentTransaction;

                var searchFilter = string.IsNullOrWhiteSpace(searchTerm)
                    ? ""
                    : @" AND ([invoice].[Invoice].[InvoiceNumber] LIKE @SearchTerm
                           OR [customer].[Customer].[Name] LIKE @SearchTerm)";

                // Escape SQL wildcards in search term
                string? escapedSearchTerm = searchTerm != null
                    ? searchTerm.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")
                    : null;

                var countQuery = $@"
                    SELECT COUNT(*)
                    FROM [invoice].[Invoice]
                    INNER JOIN [customer].[Customer]
                        ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                    LEFT JOIN (
                        SELECT [revenue].[Payment].[InvoiceId],
                               SUM([revenue].[Payment].[Amount]) AS [TotalPaid]
                        FROM [revenue].[Payment]
                        WHERE [revenue].[Payment].[IsVoided] = 0
                          AND [revenue].[Payment].[BusinessId] = @BusinessId
                        GROUP BY [revenue].[Payment].[InvoiceId]
                    ) AS ValidPayments ON [invoice].[Invoice].[Id] = ValidPayments.[InvoiceId]
                    WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                      AND [invoice].[Invoice].[IsDeleted] = 0
                      AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                      AND [invoice].[Invoice].[DueDate] < @Today
                      AND ([invoice].[Invoice].[TotalAmount] - ISNULL(ValidPayments.[TotalPaid], 0)) > 0{searchFilter}";

                var dataQuery = $@"
                    SELECT [invoice].[Invoice].[Id],
                           [invoice].[Invoice].[InvoiceNumber],
                           [customer].[Customer].[Name] AS [CustomerName],
                           [invoice].[Invoice].[DueDate],
                           DATEDIFF(DAY, [invoice].[Invoice].[DueDate], GETUTCDATE()) AS [DaysOverdue],
                           ([invoice].[Invoice].[TotalAmount] - ISNULL(ValidPayments.[TotalPaid], 0)) AS [OutstandingBalance]
                    FROM [invoice].[Invoice]
                    INNER JOIN [customer].[Customer]
                        ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                    LEFT JOIN (
                        SELECT [revenue].[Payment].[InvoiceId],
                               SUM([revenue].[Payment].[Amount]) AS [TotalPaid]
                        FROM [revenue].[Payment]
                        WHERE [revenue].[Payment].[IsVoided] = 0
                          AND [revenue].[Payment].[BusinessId] = @BusinessId
                        GROUP BY [revenue].[Payment].[InvoiceId]
                    ) AS ValidPayments ON [invoice].[Invoice].[Id] = ValidPayments.[InvoiceId]
                    WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                      AND [invoice].[Invoice].[IsDeleted] = 0
                      AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                      AND [invoice].[Invoice].[DueDate] < @Today
                      AND ([invoice].[Invoice].[TotalAmount] - ISNULL(ValidPayments.[TotalPaid], 0)) > 0{searchFilter}
                    ORDER BY [DaysOverdue] DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var offset = (page - 1) * pageSize;

                // Execute count query
                int totalCount;
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;
                    if (transaction != null) countCommand.Transaction = transaction.GetDbTransaction();

                    countCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    countCommand.Parameters.Add(new SqlParameter("@InvoiceStatusIssued", InvoiceStatusIssued));
                    countCommand.Parameters.Add(new SqlParameter("@Today", today));

                    if (!string.IsNullOrWhiteSpace(searchTerm))
                        countCommand.Parameters.Add(new SqlParameter("@SearchTerm", $"%{escapedSearchTerm}%"));

                    var countResult = await countCommand.ExecuteScalarAsync();
                    totalCount = (int)countResult!;
                }

                // Execute data query
                var items = new List<OverdueInvoiceDto>();
                using (var dataCommand = connection.CreateCommand())
                {
                    dataCommand.CommandText = dataQuery;
                    if (transaction != null) dataCommand.Transaction = transaction.GetDbTransaction();

                    dataCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    dataCommand.Parameters.Add(new SqlParameter("@InvoiceStatusIssued", InvoiceStatusIssued));
                    dataCommand.Parameters.Add(new SqlParameter("@Today", today));
                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                    if (!string.IsNullOrWhiteSpace(searchTerm))
                        dataCommand.Parameters.Add(new SqlParameter("@SearchTerm", $"%{escapedSearchTerm}%"));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        items.Add(new OverdueInvoiceDto
                        {
                            Id = reader.GetInt32(0),
                            InvoiceNumber = reader.GetString(1),
                            CustomerName = reader.GetString(2),
                            DueDate = DateOnly.FromDateTime(reader.GetDateTime(3)),
                            DaysOverdue = reader.GetInt32(4),
                            OutstandingBalance = reader.GetDecimal(5)
                        });
                    }
                }

                return new PagedResult<OverdueInvoiceDto>
                {
                    Items = items,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PagedResult<RecentPaymentDto>> GetRecentPaymentsAsync(
        int businessId, string? searchTerm, int page, int pageSize)
    {
        try
        {
            var offset = (page - 1) * pageSize;
            var (items, totalCount) = await _paymentRepository.GetRecentPaymentsPagedAsync(
                businessId, searchTerm, offset, pageSize);

            return new PagedResult<RecentPaymentDto>
            {
                Items = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ExpensesKpiDto> GetExpensesThisMonthAsync(int businessId)
    {
        try
        {
            var result = new ExpensesKpiDto();

            var connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _dbContext.Database.CurrentTransaction;

                var now = DateTime.UtcNow;
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

                const string query = @"
                    SELECT ISNULL(SUM([purchase].[Purchase].[TotalAmount]), 0) AS [TotalExpenses],
                           COUNT(*) AS [PurchaseCount]
                    FROM [purchase].[Purchase]
                    WHERE [purchase].[Purchase].[BusinessId] = @BusinessId
                      AND [purchase].[Purchase].[IsCancelled] = 0
                      AND [purchase].[Purchase].[InvoiceDate] >= @MonthStart
                      AND [purchase].[Purchase].[InvoiceDate] <= @MonthEnd";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;
                    if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                    command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    command.Parameters.Add(new SqlParameter("@MonthStart", monthStart));
                    command.Parameters.Add(new SqlParameter("@MonthEnd", monthEnd));

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        result.TotalExpenses = reader.GetDecimal(0);
                        result.PurchaseCount = reader.GetInt32(1);
                    }
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<RevenueVsExpensesDto>> GetRevenueVsExpensesAsync(int businessId)
    {
        try
        {
            var now = DateTime.UtcNow;
            // Start from the 1st of the month, 5 months ago (gives us 6 months including current)
            var sixMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

            var connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _dbContext.Database.CurrentTransaction;

                // Revenue per month (non-voided payments)
                const string revenueQuery = @"
                    SELECT YEAR([revenue].[Payment].[PaymentDateUtc]) AS [Year],
                           MONTH([revenue].[Payment].[PaymentDateUtc]) AS [Month],
                           ISNULL(SUM([revenue].[Payment].[Amount]), 0) AS [Revenue]
                    FROM [revenue].[Payment]
                    WHERE [revenue].[Payment].[BusinessId] = @BusinessId
                      AND [revenue].[Payment].[IsVoided] = 0
                      AND [revenue].[Payment].[ParentPaymentId] IS NULL
                      AND [revenue].[Payment].[PaymentDateUtc] >= @SixMonthsAgo
                    GROUP BY YEAR([revenue].[Payment].[PaymentDateUtc]), MONTH([revenue].[Payment].[PaymentDateUtc])";

                var revenueByMonth = new Dictionary<(int Year, int Month), decimal>();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = revenueQuery;
                    if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                    command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    command.Parameters.Add(new SqlParameter("@SixMonthsAgo", sixMonthsAgo));

                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var year = reader.GetInt32(0);
                        var month = reader.GetInt32(1);
                        var revenue = reader.GetDecimal(2);
                        revenueByMonth[(year, month)] = revenue;
                    }
                }

                // Expenses per month (non-cancelled purchases)
                const string expensesQuery = @"
                    SELECT YEAR([purchase].[Purchase].[InvoiceDate]) AS [Year],
                           MONTH([purchase].[Purchase].[InvoiceDate]) AS [Month],
                           ISNULL(SUM([purchase].[Purchase].[TotalAmount]), 0) AS [Expenses]
                    FROM [purchase].[Purchase]
                    WHERE [purchase].[Purchase].[BusinessId] = @BusinessId
                      AND [purchase].[Purchase].[IsCancelled] = 0
                      AND [purchase].[Purchase].[InvoiceDate] >= @SixMonthsAgo
                    GROUP BY YEAR([purchase].[Purchase].[InvoiceDate]), MONTH([purchase].[Purchase].[InvoiceDate])";

                var expensesByMonth = new Dictionary<(int Year, int Month), decimal>();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = expensesQuery;
                    if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                    command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    command.Parameters.Add(new SqlParameter("@SixMonthsAgo", sixMonthsAgo));

                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var year = reader.GetInt32(0);
                        var month = reader.GetInt32(1);
                        var expenses = reader.GetDecimal(2);
                        expensesByMonth[(year, month)] = expenses;
                    }
                }

                // Generate all 6 month entries (including months with zero data), ordered oldest to newest
                var result = new List<RevenueVsExpensesDto>();
                for (var i = 0; i < 6; i++)
                {
                    var monthDate = sixMonthsAgo.AddMonths(i);
                    var year = monthDate.Year;
                    var month = monthDate.Month;

                    result.Add(new RevenueVsExpensesDto
                    {
                        Year = year,
                        Month = month,
                        Label = monthDate.ToString("MMM", CultureInfo.InvariantCulture),
                        Revenue = revenueByMonth.TryGetValue((year, month), out var rev) ? rev : 0m,
                        Expenses = expensesByMonth.TryGetValue((year, month), out var exp) ? exp : 0m
                    });
                }

                return result;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<InvoiceStatusBreakdownDto> GetInvoiceStatusBreakdownAsync(int businessId)
    {
        try
        {
            var result = new InvoiceStatusBreakdownDto();

            var connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _dbContext.Database.CurrentTransaction;

                const string query = @"
                    SELECT [invoice].[Invoice].[InvoiceFinancialStatusTypeId],
                           COUNT(*) AS [Count]
                    FROM [invoice].[Invoice]
                    WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                      AND [invoice].[Invoice].[IsDeleted] = 0
                      AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                    GROUP BY [invoice].[Invoice].[InvoiceFinancialStatusTypeId]";

                using var command = connection.CreateCommand();
                command.CommandText = query;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@InvoiceStatusIssued", InvoiceStatusIssued));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var statusId = reader.GetInt32(0);
                    var count = reader.GetInt32(1);

                    switch (statusId)
                    {
                        case FinancialStatusUnpaid:
                            result.UnpaidCount = count;
                            break;
                        case FinancialStatusPartiallyPaid:
                            result.PartiallyPaidCount = count;
                            break;
                        case FinancialStatusPaid:
                            result.PaidCount = count;
                            break;
                        case FinancialStatusOverdue:
                            result.OverdueCount = count;
                            break;
                    }
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            result.TotalCount = result.PaidCount + result.PartiallyPaidCount + result.UnpaidCount + result.OverdueCount;

            return result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<RecentInvoiceDto>> GetRecentInvoicesAsync(int businessId)
    {
        try
        {
            var results = new List<RecentInvoiceDto>();

            var connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _dbContext.Database.CurrentTransaction;

                const string query = @"
                    SELECT TOP 5
                           [invoice].[Invoice].[Id],
                           [invoice].[Invoice].[InvoiceNumber],
                           [customer].[Customer].[Name] AS [CustomerName],
                           [invoice].[Invoice].[InvoiceFinancialStatusTypeId],
                           [invoice].[InvoiceFinancialStatusType].[Name] AS [FinancialStatusName],
                           [invoice].[Invoice].[TotalAmount]
                    FROM [invoice].[Invoice]
                    INNER JOIN [customer].[Customer]
                        ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                    INNER JOIN [invoice].[InvoiceFinancialStatusType]
                        ON [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = [invoice].[InvoiceFinancialStatusType].[Id]
                    WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                      AND [invoice].[Invoice].[IsDeleted] = 0
                      AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                    ORDER BY [invoice].[Invoice].[InvoiceDate] DESC";

                using var command = connection.CreateCommand();
                command.CommandText = query;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@InvoiceStatusIssued", InvoiceStatusIssued));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new RecentInvoiceDto
                    {
                        Id = reader.GetInt32(0),
                        InvoiceNumber = reader.GetString(1),
                        CustomerName = reader.GetString(2),
                        InvoiceFinancialStatusTypeId = reader.GetInt32(3),
                        FinancialStatusName = reader.GetString(4),
                        TotalAmount = reader.GetDecimal(5)
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<VatSummaryDto> GetVatSummaryAsync(int businessId)
    {
        try
        {
            var result = new VatSummaryDto
            {
                TotalOutputVat = 0m,
                TotalInputVat = 0m,
                NetVatPayable = 0m,
                PeriodLabel = string.Empty,
                HasData = false
            };

            var connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _dbContext.Database.CurrentTransaction;

                const string query = @"
                    SELECT TOP 1
                           [vat].[VatSubmission].[TotalOutputVat],
                           [vat].[VatSubmission].[TotalInputVat],
                           [vat].[VatSubmission].[NetVatPayable],
                           [vat].[VatSubmissionPeriod].[PeriodLabel]
                    FROM [vat].[VatSubmission]
                    INNER JOIN [vat].[VatSubmissionPeriod]
                        ON [vat].[VatSubmission].[VatSubmissionPeriodId] = [vat].[VatSubmissionPeriod].[Id]
                    WHERE [vat].[VatSubmission].[BusinessId] = @BusinessId
                    ORDER BY
                        CASE WHEN [vat].[VatSubmission].[IsSubmitted] = 0 THEN 0 ELSE 1 END,
                        [vat].[VatSubmissionPeriod].[PeriodEndDate] DESC";

                using var command = connection.CreateCommand();
                command.CommandText = query;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.TotalOutputVat = reader.GetDecimal(0);
                    result.TotalInputVat = reader.GetDecimal(1);
                    result.NetVatPayable = reader.GetDecimal(2);
                    result.PeriodLabel = reader.GetString(3);
                    result.HasData = true;
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<TopCustomerDto>> GetTopCustomersAsync(int businessId)
    {
        try
        {
            var results = new List<TopCustomerDto>();

            var connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _dbContext.Database.CurrentTransaction;

                const string query = @"
                    SELECT TOP 5
                           [customer].[Customer].[Id] AS [CustomerId],
                           [customer].[Customer].[Name] AS [CustomerName],
                           ISNULL(SUM([invoice].[Invoice].[TotalAmount]), 0) AS [TotalInvoiced],
                           ISNULL(SUM(ValidPayments.[TotalPaid]), 0) AS [TotalPaid]
                    FROM [invoice].[Invoice]
                    INNER JOIN [customer].[Customer]
                        ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                    LEFT JOIN (
                        SELECT [revenue].[Payment].[InvoiceId],
                               SUM([revenue].[Payment].[Amount]) AS [TotalPaid]
                        FROM [revenue].[Payment]
                        WHERE [revenue].[Payment].[IsVoided] = 0
                          AND [revenue].[Payment].[BusinessId] = @BusinessId
                        GROUP BY [revenue].[Payment].[InvoiceId]
                    ) AS ValidPayments ON [invoice].[Invoice].[Id] = ValidPayments.[InvoiceId]
                    WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                      AND [invoice].[Invoice].[IsDeleted] = 0
                      AND [invoice].[Invoice].[InvoiceStatusTypeId] = @InvoiceStatusIssued
                      AND [invoice].[Invoice].[InvoiceDate] >= @TwelveMonthsAgo
                    GROUP BY [customer].[Customer].[Id], [customer].[Customer].[Name]
                    ORDER BY [TotalInvoiced] DESC";

                using var command = connection.CreateCommand();
                command.CommandText = query;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@InvoiceStatusIssued", InvoiceStatusIssued));
                command.Parameters.Add(new SqlParameter("@TwelveMonthsAgo", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-12))));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new TopCustomerDto
                    {
                        CustomerId = reader.GetInt32(0),
                        CustomerName = reader.GetString(1),
                        TotalInvoiced = reader.GetDecimal(2),
                        TotalPaid = reader.GetDecimal(3)
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
