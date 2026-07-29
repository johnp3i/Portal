using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Stateless computation service for Profit &amp; Loss reporting.
/// Computes Revenue, COGS, Operating Expenses, derived figures, category breakdown,
/// and year-over-year trend comparison from existing Payment and Purchase data.
/// </summary>
public class PnlService : IPnlService
{
    private readonly PortalDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;

    // PurchaseType constants
    private const int PurchaseTypeStock = 2;  // COGS
    private const int PurchaseTypeExpense = 3; // Operating Expenses

    public PnlService(PortalDbContext dbContext, ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
    }

    /// <inheritdoc />
    public async Task<PnlSummaryDto> GetSummaryAsync(PnlPeriodRequest request)
    {
        try
        {
            // Resolve the period dates
            var dateRange = request.PeriodType == PnlPeriodType.Custom
                ? new PnlDateRange
                {
                    StartDate = request.CustomStartDate!.Value,
                    EndDate = request.CustomEndDate!.Value
                }
                : ResolvePeriod(request.PeriodType, DateTime.UtcNow);

            var businessId = _currentTenantService.CurrentBusinessId;

            // Compute current period figures
            var revenue = await ComputeRevenueAsync(dateRange.StartDate, dateRange.EndDate, businessId);
            var cogs = await ComputePurchaseAmountAsync(dateRange.StartDate, dateRange.EndDate, businessId, PurchaseTypeStock);
            var operatingExpenses = await ComputePurchaseAmountAsync(dateRange.StartDate, dateRange.EndDate, businessId, PurchaseTypeExpense);

            // Derived figures
            var grossProfit = revenue - cogs;
            var netProfit = grossProfit - operatingExpenses;
            var grossMargin = revenue == 0 ? 0m : (grossProfit / revenue) * 100m;
            var netMargin = revenue == 0 ? 0m : (netProfit / revenue) * 100m;

            // Category breakdown
            var categoryBreakdown = await ComputeCategoryBreakdownAsync(dateRange.StartDate, dateRange.EndDate, businessId);

            // Trend comparison (shift period back 12 months)
            var trend = await ComputeTrendAsync(dateRange.StartDate, dateRange.EndDate, businessId, revenue, cogs, grossProfit, operatingExpenses, netProfit);

            return new PnlSummaryDto
            {
                PeriodStart = dateRange.StartDate,
                PeriodEnd = dateRange.EndDate,
                Revenue = revenue,
                Cogs = cogs,
                GrossProfit = grossProfit,
                OperatingExpenses = operatingExpenses,
                NetProfit = netProfit,
                GrossMargin = grossMargin,
                NetMargin = netMargin,
                Trend = trend,
                CategoryBreakdown = categoryBreakdown,
                HasData = revenue != 0 || cogs != 0 || operatingExpenses != 0
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public PnlDateRange ResolvePeriod(PnlPeriodType periodType, DateTime referenceDate)
    {
        return periodType switch
        {
            PnlPeriodType.CurrentMonth => new PnlDateRange
            {
                StartDate = new DateOnly(referenceDate.Year, referenceDate.Month, 1),
                EndDate = new DateOnly(referenceDate.Year, referenceDate.Month, DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month))
            },
            PnlPeriodType.PreviousMonth => ResolvePreviousMonth(referenceDate),
            PnlPeriodType.CurrentQuarter => new PnlDateRange
            {
                StartDate = GetQuarterStart(referenceDate),
                EndDate = DateOnly.FromDateTime(referenceDate)
            },
            PnlPeriodType.CurrentYear => new PnlDateRange
            {
                StartDate = new DateOnly(referenceDate.Year, 1, 1),
                EndDate = DateOnly.FromDateTime(referenceDate)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(periodType), periodType, "Use Custom period type with explicit dates.")
        };
    }

    /// <inheritdoc />
    public PnlValidationResult ValidateCustomRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
        {
            return new PnlValidationResult
            {
                IsValid = false,
                ErrorMessage = "Start date must be before or equal to end date."
            };
        }

        return new PnlValidationResult { IsValid = true };
    }

    #region Private Methods

    private async Task<decimal> ComputeRevenueAsync(DateOnly startDate, DateOnly endDate, int businessId)
    {
        // PaymentDateUtc is DateTime, so we compare:
        // PaymentDateUtc >= startDate (as DateTime) AND PaymentDateUtc < endDate + 1 day (as DateTime)
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MinValue).AddDays(1);

        var revenue = await _dbContext.Payments
            .Where(p => p.BusinessId == businessId
                        && !p.IsVoided
                        && p.ParentPaymentId == null
                        && p.PaymentDateUtc >= startDateTime
                        && p.PaymentDateUtc < endDateTime)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        return revenue;
    }

    private async Task<decimal> ComputePurchaseAmountAsync(DateOnly startDate, DateOnly endDate, int businessId, int purchaseTypeId)
    {
        var amount = await _dbContext.Purchases
            .Where(p => p.BusinessId == businessId
                        && !p.IsCancelled
                        && p.PurchaseTypeId == purchaseTypeId
                        && p.InvoiceDate >= startDate
                        && p.InvoiceDate <= endDate)
            .SumAsync(p => (decimal?)p.TotalAmount) ?? 0m;

        return amount;
    }

    private async Task<List<PnlCategoryBreakdownDto>> ComputeCategoryBreakdownAsync(DateOnly startDate, DateOnly endDate, int businessId)
    {
        var purchases = await _dbContext.Purchases
            .Include(p => p.ExpenseCategory)
                .ThenInclude(ec => ec.ExpenseType)
            .Include(p => p.PurchaseType)
            .Where(p => p.BusinessId == businessId
                        && !p.IsCancelled
                        && (p.PurchaseTypeId == PurchaseTypeStock || p.PurchaseTypeId == PurchaseTypeExpense)
                        && p.InvoiceDate >= startDate
                        && p.InvoiceDate <= endDate)
            .ToListAsync();

        var totalExpenses = purchases.Sum(p => p.TotalAmount);

        if (totalExpenses == 0)
            return new List<PnlCategoryBreakdownDto>();

        var breakdown = purchases
            .GroupBy(p => new
            {
                p.ExpenseCategoryId,
                CategoryName = p.ExpenseCategory.Name,
                ExpenseTypeName = p.ExpenseCategory.ExpenseType?.Name ?? "Uncategorised",
                p.PurchaseTypeId,
                PurchaseTypeName = p.PurchaseType.Name
            })
            .Select(g => new PnlCategoryBreakdownDto
            {
                ExpenseCategoryId = g.Key.ExpenseCategoryId,
                CategoryName = g.Key.CategoryName,
                ExpenseTypeName = g.Key.ExpenseTypeName,
                PurchaseTypeId = g.Key.PurchaseTypeId,
                PurchaseTypeName = g.Key.PurchaseTypeName,
                TotalAmount = g.Sum(p => p.TotalAmount),
                PercentageOfTotal = (g.Sum(p => p.TotalAmount) / totalExpenses) * 100m
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        return breakdown;
    }

    private async Task<PnlTrendDto?> ComputeTrendAsync(
        DateOnly startDate, DateOnly endDate, int businessId,
        decimal currentRevenue, decimal currentCogs, decimal currentGrossProfit,
        decimal currentOperatingExpenses, decimal currentNetProfit)
    {
        // Shift period back by one year
        var previousStartDate = startDate.AddYears(-1);
        var previousEndDate = endDate.AddYears(-1);

        var previousRevenue = await ComputeRevenueAsync(previousStartDate, previousEndDate, businessId);
        var previousCogs = await ComputePurchaseAmountAsync(previousStartDate, previousEndDate, businessId, PurchaseTypeStock);
        var previousOperatingExpenses = await ComputePurchaseAmountAsync(previousStartDate, previousEndDate, businessId, PurchaseTypeExpense);
        var previousGrossProfit = previousRevenue - previousCogs;
        var previousNetProfit = previousGrossProfit - previousOperatingExpenses;

        return new PnlTrendDto
        {
            PreviousRevenue = previousRevenue,
            PreviousCogs = previousCogs,
            PreviousGrossProfit = previousGrossProfit,
            PreviousOperatingExpenses = previousOperatingExpenses,
            PreviousNetProfit = previousNetProfit,
            RevenueChange = ComputePercentageChange(currentRevenue, previousRevenue),
            CogsChange = ComputePercentageChange(currentCogs, previousCogs),
            GrossProfitChange = ComputePercentageChange(currentGrossProfit, previousGrossProfit),
            OperatingExpensesChange = ComputePercentageChange(currentOperatingExpenses, previousOperatingExpenses),
            NetProfitChange = ComputePercentageChange(currentNetProfit, previousNetProfit)
        };
    }

    private static decimal? ComputePercentageChange(decimal current, decimal previous)
    {
        if (previous == 0)
            return null;

        return ((current - previous) / Math.Abs(previous)) * 100m;
    }

    private static PnlDateRange ResolvePreviousMonth(DateTime referenceDate)
    {
        var previousMonth = referenceDate.AddMonths(-1);
        return new PnlDateRange
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

    #endregion
}
