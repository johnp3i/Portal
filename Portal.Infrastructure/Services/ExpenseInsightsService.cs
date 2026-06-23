using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.ExpenseInsights;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides expense analytics computations including category breakdown, trend analysis,
/// budget management, and CSV export. All queries are scoped to the current tenant
/// via ICurrentTenantService.
/// </summary>
public class ExpenseInsightsService : IExpenseInsightsService
{
    private readonly PortalDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;

    public ExpenseInsightsService(PortalDbContext dbContext, ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
    }

    /// <inheritdoc />
    public async Task<ExpenseInsightsDto> GetInsightsDataAsync(ExpenseInsightsPeriodRequest request)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            if (businessId == 0) return ExpenseInsightsDto.Empty();

            var dateRange = request.PeriodType == PnlPeriodType.Custom
                ? new ExpenseInsightsDateRange
                {
                    StartDate = request.CustomStartDate!.Value,
                    EndDate = request.CustomEndDate!.Value
                }
                : ResolvePeriod(request.PeriodType, DateTime.UtcNow);

            // 1. Fetch non-cancelled purchases in period for this business
            var purchases = await _dbContext.Purchases
                .Include(p => p.ExpenseCategory)
                    .ThenInclude(ec => ec.ExpenseType)
                .Include(p => p.Supplier)
                .Where(p => p.BusinessId == businessId
                            && !p.IsCancelled
                            && p.InvoiceDate >= dateRange.StartDate
                            && p.InvoiceDate <= dateRange.EndDate)
                .ToListAsync();

            // 2. Compute category breakdown
            var totalSpend = purchases.Sum(p => p.TotalAmount);
            var breakdown = ComputeCategoryBreakdown(purchases, totalSpend);

            // 3. Compute MoM variance
            var previousMonthPurchases = await GetPreviousMonthPurchasesAsync(dateRange, businessId);
            EnrichWithVariance(breakdown, previousMonthPurchases);

            // 4. Compute top suppliers per category
            EnrichWithTopSuppliers(breakdown, purchases);

            // 5. Fetch budget limits and enrich with budget status
            var limits = await _dbContext.ExpenseCategoryLimits
                .Where(l => l.BusinessId == businessId)
                .ToListAsync();
            EnrichWithBudgetStatus(breakdown, limits);

            // 6. Compute budget alert counts
            var budgetExceededCount = breakdown.Count(c => c.BudgetStatus == "Exceeded");
            var budgetApproachingCount = breakdown.Count(c => c.BudgetStatus == "Approaching");

            // 7. Build summary KPIs
            var summary = new ExpenseInsightsSummaryDto
            {
                TotalSpend = totalSpend,
                CategoriesWithSpend = breakdown.Count,
                TopCategoryName = breakdown.FirstOrDefault()?.CategoryName,
                AveragePerCategory = breakdown.Count > 0 ? Math.Round(totalSpend / breakdown.Count, 2) : 0
            };

            return new ExpenseInsightsDto
            {
                Summary = summary,
                Categories = breakdown,
                Period = dateRange,
                BudgetExceededCount = budgetExceededCount,
                BudgetApproachingCount = budgetApproachingCount,
                HasData = purchases.Any()
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ExpenseInsightsTrendDto> GetTrendDataAsync()
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            if (businessId == 0) return new ExpenseInsightsTrendDto();

            var now = DateTime.UtcNow;
            var endMonth = new DateOnly(now.Year, now.Month, 1);
            var startMonth = endMonth.AddMonths(-11); // 12 months total

            // Generate month labels using invariant culture for consistent "MMM yyyy" format
            var monthLabels = new List<string>();
            for (int i = 0; i < 12; i++)
            {
                var month = startMonth.AddMonths(i);
                monthLabels.Add(month.ToString("MMM yyyy", CultureInfo.InvariantCulture));
            }

            // Compute the full date window for querying purchases
            var windowStart = startMonth;
            var windowEnd = new DateOnly(endMonth.Year, endMonth.Month, DateTime.DaysInMonth(endMonth.Year, endMonth.Month));

            // Fetch all non-cancelled purchases in the 12-month window for this business
            var purchases = await _dbContext.Purchases
                .Include(p => p.ExpenseCategory)
                .Where(p => p.BusinessId == businessId
                            && !p.IsCancelled
                            && p.InvoiceDate >= windowStart
                            && p.InvoiceDate <= windowEnd)
                .ToListAsync();

            // Group by category, compute 12-month totals, take top 5 with spend > 0
            var categoryTotals = purchases
                .GroupBy(p => new { p.ExpenseCategoryId, p.ExpenseCategory.Name })
                .Select(g => new { g.Key.ExpenseCategoryId, g.Key.Name, Total = g.Sum(p => p.TotalAmount) })
                .Where(c => c.Total > 0)
                .OrderByDescending(c => c.Total)
                .Take(5)
                .ToList();

            // Determine HasSufficientData — need at least 2 distinct months with data
            var distinctMonths = purchases
                .Select(p => new { p.InvoiceDate.Year, p.InvoiceDate.Month })
                .Distinct()
                .Count();
            var hasSufficientData = distinctMonths >= 2;

            // Build series — one per top-5 category with monthly totals (12 values each)
            var series = new List<TrendCategorySeriesDto>();
            foreach (var cat in categoryTotals)
            {
                var monthlyTotals = new List<decimal>();
                var catPurchases = purchases.Where(p => p.ExpenseCategoryId == cat.ExpenseCategoryId);

                for (int i = 0; i < 12; i++)
                {
                    var monthStart = startMonth.AddMonths(i);
                    var monthEnd = new DateOnly(monthStart.Year, monthStart.Month,
                        DateTime.DaysInMonth(monthStart.Year, monthStart.Month));
                    var monthTotal = catPurchases
                        .Where(p => p.InvoiceDate >= monthStart && p.InvoiceDate <= monthEnd)
                        .Sum(p => p.TotalAmount);
                    monthlyTotals.Add(monthTotal);
                }

                series.Add(new TrendCategorySeriesDto { CategoryName = cat.Name, MonthlyTotals = monthlyTotals });
            }

            return new ExpenseInsightsTrendDto
            {
                MonthLabels = monthLabels,
                Series = series,
                HasSufficientData = hasSufficientData
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpsertBudgetLimitAsync(int expenseCategoryId, decimal? periodLimitEur)
    {
        try
        {
            if (periodLimitEur.HasValue && (periodLimitEur.Value <= 0 || periodLimitEur.Value > 999_999_999.99m))
                return ServiceResult.Fail("Budget limit must be between 0.01 and 999,999,999.99.");

            var businessId = _currentTenantService.CurrentBusinessId;
            if (businessId == 0) return ServiceResult.Fail("No active business context.");

            var existing = await _dbContext.ExpenseCategoryLimits
                .FirstOrDefaultAsync(l => l.ExpenseCategoryId == expenseCategoryId && l.BusinessId == businessId);

            if (existing != null)
            {
                existing.PeriodLimitEur = periodLimitEur;
            }
            else
            {
                var newLimit = new ExpenseCategoryLimit
                {
                    BusinessId = businessId,
                    ExpenseCategoryId = expenseCategoryId,
                    PeriodLimitEur = periodLimitEur
                };
                _dbContext.ExpenseCategoryLimits.Add(newLimit);
            }

            await _dbContext.SaveChangesAsync();
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public ExpenseInsightsDateRange ResolvePeriod(PnlPeriodType periodType, DateTime referenceDate)
    {
        // Stub — will be fully implemented in task 2.2
        return periodType switch
        {
            PnlPeriodType.CurrentMonth => new ExpenseInsightsDateRange
            {
                StartDate = new DateOnly(referenceDate.Year, referenceDate.Month, 1),
                EndDate = DateOnly.FromDateTime(referenceDate)
            },
            PnlPeriodType.PreviousMonth => ResolvePreviousMonth(referenceDate),
            PnlPeriodType.CurrentQuarter => new ExpenseInsightsDateRange
            {
                StartDate = GetQuarterStart(referenceDate),
                EndDate = DateOnly.FromDateTime(referenceDate)
            },
            PnlPeriodType.CurrentYear => new ExpenseInsightsDateRange
            {
                StartDate = new DateOnly(referenceDate.Year, 1, 1),
                EndDate = DateOnly.FromDateTime(referenceDate)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(periodType), periodType, "Use Custom period type with explicit dates.")
        };
    }

    /// <inheritdoc />
    public ExpenseInsightsValidationResult ValidateCustomRange(DateOnly startDate, DateOnly endDate)
    {
        // Stub — will be fully implemented in task 2.2
        if (startDate > endDate)
        {
            return new ExpenseInsightsValidationResult
            {
                IsValid = false,
                ErrorMessage = "Start date must be before or equal to end date."
            };
        }

        var daysDiff = endDate.DayNumber - startDate.DayNumber;
        if (daysDiff > 366)
        {
            return new ExpenseInsightsValidationResult
            {
                IsValid = false,
                ErrorMessage = "Date range must not exceed 366 days."
            };
        }

        return new ExpenseInsightsValidationResult { IsValid = true };
    }

    /// <inheritdoc />
    public async Task<ExportResult> ExportCsvAsync(ExpenseInsightsPeriodRequest request)
    {
        try
        {
            var data = await GetInsightsDataAsync(request);
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("Category Name,Expense Type,Total Spend,Percentage of Total,Month-Over-Month Variance,Budget Limit,Budget Status");

            foreach (var cat in data.Categories)
            {
                sb.AppendLine($"{EscapeCsv(cat.CategoryName)},{EscapeCsv(cat.ExpenseTypeName)},{cat.TotalSpend:F2},{cat.PercentageOfTotal:F1},{FormatVarianceForCsv(cat.Variance)},{FormatBudgetLimit(cat.BudgetLimit)},{EscapeCsv(cat.BudgetStatus)}");
            }

            var businessName = SanitizeFileName(await GetBusinessNameAsync());
            var filename = $"ExpenseInsights_{businessName}_{data.Period.StartDate:yyyyMMdd}_{data.Period.EndDate:yyyyMMdd}.csv";

            return new ExportResult
            {
                Content = Encoding.UTF8.GetBytes(sb.ToString()),
                FileName = filename,
                ContentType = "text/csv"
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #region Private Methods

    /// <summary>
    /// Groups purchases by ExpenseCategoryId and computes spend totals and percentages.
    /// Includes inactive categories if they have purchases in the period.
    /// Returns empty list when totalSpend is zero.
    /// </summary>
    private static List<ExpenseCategoryBreakdownDto> ComputeCategoryBreakdown(
        List<Purchase> purchases, decimal totalSpend)
    {
        if (totalSpend == 0) return new List<ExpenseCategoryBreakdownDto>();

        return purchases
            .GroupBy(p => new
            {
                p.ExpenseCategoryId,
                CategoryName = p.ExpenseCategory.Name,
                ExpenseTypeName = p.ExpenseCategory.ExpenseType?.Name
            })
            .Select(g => new ExpenseCategoryBreakdownDto
            {
                ExpenseCategoryId = g.Key.ExpenseCategoryId,
                CategoryName = g.Key.CategoryName,
                ExpenseTypeName = g.Key.ExpenseTypeName ?? "Uncategorised",
                TotalSpend = g.Sum(p => p.TotalAmount),
                PercentageOfTotal = Math.Round((g.Sum(p => p.TotalAmount) / totalSpend) * 100m, 2)
            })
            .OrderByDescending(c => c.TotalSpend)
            .ToList();
    }

    /// <summary>
    /// Classifies a category's budget status based on its spend relative to the configured limit.
    /// </summary>
    private static BudgetStatus ComputeBudgetStatus(decimal spend, decimal? limit)
    {
        if (limit == null || limit <= 0) return BudgetStatus.NoLimit;
        var ratio = spend / limit.Value;
        if (ratio >= 1.0m) return BudgetStatus.Exceeded;
        if (ratio >= 0.8m) return BudgetStatus.Approaching;
        return BudgetStatus.WithinLimit;
    }

    /// <summary>
    /// Maps BudgetStatus enum values to their display string equivalents.
    /// </summary>
    private static string BudgetStatusToDisplayString(BudgetStatus status)
    {
        return status switch
        {
            BudgetStatus.Exceeded => "Exceeded",
            BudgetStatus.Approaching => "Approaching",
            BudgetStatus.WithinLimit => "Within Limit",
            BudgetStatus.NoLimit => "No Limit",
            _ => "No Limit"
        };
    }

    /// <summary>
    /// Enriches category breakdown items with budget limit and status from ExpenseCategoryLimit records.
    /// </summary>
    private static void EnrichWithBudgetStatus(
        List<ExpenseCategoryBreakdownDto> breakdown,
        List<ExpenseCategoryLimit> limits)
    {
        foreach (var category in breakdown)
        {
            var limitRecord = limits.FirstOrDefault(l => l.ExpenseCategoryId == category.ExpenseCategoryId);
            if (limitRecord != null)
            {
                category.BudgetLimit = limitRecord.PeriodLimitEur;
                var status = ComputeBudgetStatus(category.TotalSpend, limitRecord.PeriodLimitEur);
                category.BudgetStatus = BudgetStatusToDisplayString(status);
            }
            else
            {
                category.BudgetLimit = null;
                category.BudgetStatus = BudgetStatusToDisplayString(BudgetStatus.NoLimit);
            }
        }
    }

    private static ExpenseInsightsDateRange ResolvePreviousMonth(DateTime referenceDate)
    {
        var previousMonth = referenceDate.AddMonths(-1);
        return new ExpenseInsightsDateRange
        {
            StartDate = new DateOnly(previousMonth.Year, previousMonth.Month, 1),
            EndDate = new DateOnly(previousMonth.Year, previousMonth.Month, DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month))
        };
    }

    private static DateOnly GetQuarterStart(DateTime referenceDate)
    {
        var quarterMonth = ((referenceDate.Month - 1) / 3) * 3 + 1;
        return new DateOnly(referenceDate.Year, quarterMonth, 1);
    }

    /// <summary>
    /// Fetches non-cancelled purchases for the calendar month immediately preceding
    /// the period's start date. Used for Month-Over-Month variance computation.
    /// </summary>
    private async Task<List<Purchase>> GetPreviousMonthPurchasesAsync(ExpenseInsightsDateRange dateRange, int businessId)
    {
        try
        {
            var previousMonthDate = dateRange.StartDate.ToDateTime(TimeOnly.MinValue).AddMonths(-1);
            var previousMonthStart = new DateOnly(previousMonthDate.Year, previousMonthDate.Month, 1);
            var previousMonthEnd = new DateOnly(previousMonthDate.Year, previousMonthDate.Month,
                DateTime.DaysInMonth(previousMonthDate.Year, previousMonthDate.Month));

            return await _dbContext.Purchases
                .Where(p => p.BusinessId == businessId
                            && !p.IsCancelled
                            && p.InvoiceDate >= previousMonthStart
                            && p.InvoiceDate <= previousMonthEnd)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Enriches each category breakdown entry with Month-Over-Month variance data
    /// by comparing current spend to the previous month's spend per category.
    /// </summary>
    private static void EnrichWithVariance(
        List<ExpenseCategoryBreakdownDto> breakdown,
        List<Purchase> previousMonthPurchases)
    {
        var hasPreviousData = previousMonthPurchases.Any();

        var previousSpendByCategory = previousMonthPurchases
            .GroupBy(p => p.ExpenseCategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.TotalAmount));

        foreach (var category in breakdown)
        {
            var previousSpend = previousSpendByCategory.GetValueOrDefault(category.ExpenseCategoryId, 0m);
            var varianceText = ComputeVariance(category.TotalSpend, previousSpend, hasPreviousData);
            category.Variance = varianceText;

            // Set VarianceValue for numeric cases (used for sorting/styling)
            if (decimal.TryParse(varianceText, out var numericValue))
            {
                category.VarianceValue = numericValue;
            }
            else
            {
                category.VarianceValue = null;
            }
        }
    }

    /// <summary>
    /// Computes the Month-Over-Month variance string for a single category.
    /// Returns special labels for edge cases, or the percentage change rounded to 1dp.
    /// </summary>
    private static string ComputeVariance(decimal currentSpend, decimal previousSpend, bool hasPreviousData)
    {
        if (!hasPreviousData) return "N/A";
        if (previousSpend == 0 && currentSpend > 0) return "New";
        if (previousSpend == 0 && currentSpend == 0) return "—";
        if (currentSpend == 0 && previousSpend > 0) return "-100.0";

        var variance = Math.Round(((currentSpend - previousSpend) / previousSpend) * 100m, 1);
        return variance.ToString("F1");
    }

    /// <summary>
    /// Enriches each category breakdown entry with the top 3 suppliers by spend.
    /// Groups the original purchases list by ExpenseCategoryId, then computes top suppliers
    /// for each category in the breakdown.
    /// </summary>
    private static void EnrichWithTopSuppliers(
        List<ExpenseCategoryBreakdownDto> breakdown,
        List<Purchase> purchases)
    {
        try
        {
            var purchasesByCategory = purchases
                .GroupBy(p => p.ExpenseCategoryId)
                .ToDictionary(g => g.Key, g => g.AsEnumerable());

            foreach (var category in breakdown)
            {
                if (purchasesByCategory.TryGetValue(category.ExpenseCategoryId, out var categoryPurchases))
                {
                    category.TopSuppliers = ComputeTopSuppliers(categoryPurchases, category.TotalSpend);
                }
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Computes the top 3 suppliers for a set of category purchases, ordered by total spend
    /// descending with SupplierId ascending as tie-breaker. Returns fewer than 3 if the
    /// category has fewer suppliers. Returns empty list if category total is zero.
    /// </summary>
    private static List<TopSupplierDto> ComputeTopSuppliers(
        IEnumerable<Purchase> categoryPurchases, decimal categoryTotal)
    {
        if (categoryTotal == 0) return new List<TopSupplierDto>();

        return categoryPurchases
            .GroupBy(p => new { p.SupplierId, p.Supplier.Name })
            .Select(g => new TopSupplierDto
            {
                SupplierId = g.Key.SupplierId,
                SupplierName = g.Key.Name,
                TotalSpend = g.Sum(p => p.TotalAmount),
                PercentageOfCategory = Math.Round((g.Sum(p => p.TotalAmount) / categoryTotal) * 100m, 1)
            })
            .OrderByDescending(s => s.TotalSpend)
            .ThenBy(s => s.SupplierId)
            .Take(3)
            .ToList();
    }

    /// <summary>
    /// Escapes a CSV field value per RFC 4180. Wraps in double quotes if the value contains
    /// a comma, double quote, or newline. Internal double quotes are escaped by doubling them.
    /// </summary>
    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    /// <summary>
    /// Formats the variance value for CSV output. Numeric values are formatted to 2dp;
    /// non-numeric special labels (N/A, New, —) are output as-is.
    /// </summary>
    private static string FormatVarianceForCsv(string? variance)
    {
        if (string.IsNullOrEmpty(variance)) return string.Empty;

        if (decimal.TryParse(variance, out var numericValue))
        {
            return numericValue.ToString("F2");
        }

        return variance;
    }

    /// <summary>
    /// Formats a budget limit value to 2 decimal places, or returns empty string if null.
    /// </summary>
    private static string FormatBudgetLimit(decimal? limit)
    {
        return limit.HasValue ? limit.Value.ToString("F2") : string.Empty;
    }

    /// <summary>
    /// Sanitizes a string for use in a filename by replacing spaces with underscores
    /// and removing any characters that are not alphanumeric or underscores.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";

        var sanitized = name.Replace(' ', '_');
        sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9_]", string.Empty);

        return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
    }

    /// <summary>
    /// Fetches the business name for the current tenant from the database.
    /// </summary>
    private async Task<string> GetBusinessNameAsync()
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var business = await _dbContext.Businesses
                .Where(b => b.Id == businessId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            return business ?? "Unknown";
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion
}
