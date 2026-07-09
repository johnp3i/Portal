using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.CashFlow;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Computes cash flow projections on-demand from live Invoice, Payment, Purchase, and Settings data.
/// All queries are scoped to the specified businessId for tenant isolation.
/// </summary>
public class CashFlowService : ICashFlowService
{
    private readonly PortalDbContext _dbContext;

    // Financial status constants for eligible invoices
    private static readonly int[] EligibleFinancialStatuses = { 1, 2, 4 }; // Unpaid, PartiallyPaid, Overdue

    public CashFlowService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<CashFlowProjectionDto> GetProjectionAsync(int businessId, int daysAhead = 30, int[]? excludedInvoiceIds = null)
    {
        try
        {
            // 1. Load settings (or use defaults)
            var settings = await _dbContext.CashFlowSettings
                .FirstOrDefaultAsync(s => s.BusinessId == businessId);

            var startingBalance = settings?.StartingBalance ?? 0m;
            var alertThreshold = settings?.AlertThreshold ?? 0m;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var endDate = today.AddDays(daysAhead);
            var excludedSet = excludedInvoiceIds?.ToHashSet() ?? new HashSet<int>();

            // 2. Compute inflows — outstanding invoices with confidence weighting
            var invoices = await _dbContext.Invoices
                .Include(i => i.Customer)
                .Where(i => i.BusinessId == businessId
                           && !i.IsDeleted
                           && i.InvoiceStatusTypeId == 2 // Issued only
                           && EligibleFinancialStatuses.Contains(i.InvoiceFinancialStatusTypeId))
                .ToListAsync();

            // Compute DaysLateAverage per customer
            var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
            var customerDaysLate = new Dictionary<int, int>();

            foreach (var customerId in customerIds)
            {
                var payments = await _dbContext.Payments
                    .Where(p => p.BusinessId == businessId
                               && !p.IsVoided
                               && p.Invoice != null
                               && p.Invoice.CustomerId == customerId)
                    .Select(p => new { p.PaymentDateUtc, p.Invoice.DueDate })
                    .ToListAsync();

                if (payments.Count == 0)
                {
                    customerDaysLate[customerId] = 0;
                }
                else
                {
                    var avgDaysLate = payments
                        .Select(p => Math.Max(0, (p.PaymentDateUtc.Date - p.DueDate.ToDateTime(TimeOnly.MinValue)).Days))
                        .Average();
                    customerDaysLate[customerId] = (int)Math.Round(avgDaysLate);
                }
            }

            // Build inflow items
            var inflows = new List<InflowItemDto>();
            foreach (var invoice in invoices)
            {
                if (excludedSet.Contains(invoice.Id)) continue;

                // Calculate outstanding amount
                decimal outstandingAmount;
                if (invoice.InvoiceFinancialStatusTypeId == 2) // PartiallyPaid
                {
                    var totalPaid = await _dbContext.Payments
                        .Where(p => p.InvoiceId == invoice.Id && !p.IsVoided)
                        .SumAsync(p => (decimal?)p.Amount) ?? 0m;
                    outstandingAmount = invoice.TotalAmount - totalPaid;
                }
                else
                {
                    outstandingAmount = invoice.TotalAmount;
                }

                if (outstandingAmount <= 0) continue;

                var daysLate = customerDaysLate.GetValueOrDefault(invoice.CustomerId, 0);
                var originalDueDate = invoice.DueDate;
                var adjustedDueDate = originalDueDate.AddDays(daysLate);

                // Floor at today — never position in the past
                if (adjustedDueDate < today)
                    adjustedDueDate = today;

                // Only include if within horizon
                if (adjustedDueDate > endDate) continue;

                inflows.Add(new InflowItemDto
                {
                    InvoiceId = invoice.Id,
                    CustomerName = invoice.Customer?.Name ?? "Unknown",
                    InvoiceNumber = invoice.InvoiceNumber,
                    OutstandingAmount = outstandingAmount,
                    OriginalDueDate = originalDueDate,
                    AdjustedDueDate = adjustedDueDate,
                    DaysLateAverage = daysLate
                });
            }

            // Sort inflows by AdjustedDueDate ascending
            inflows = inflows.OrderBy(i => i.AdjustedDueDate).ToList();

            // 3. Compute outflows — 6-month historical averages per expense category
            var sixMonthsAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6));
            var purchases = await _dbContext.Purchases
                .Where(p => p.BusinessId == businessId
                           && p.InvoiceDate >= sixMonthsAgo
                           && !p.IsCancelled)
                .Select(p => new { p.ExpenseCategoryId, p.TotalAmount, p.InvoiceDate })
                .ToListAsync();

            var outflows = new List<OutflowCategoryDto>();
            var categoryGroups = purchases.GroupBy(p => p.ExpenseCategoryId);

            foreach (var group in categoryGroups)
            {
                var distinctMonths = group
                    .Select(p => new { p.InvoiceDate.Year, p.InvoiceDate.Month })
                    .Distinct()
                    .Count();

                // Exclude categories with fewer than 2 months of data
                if (distinctMonths < 2) continue;

                var totalAmount = group.Sum(p => p.TotalAmount);
                var monthlyAverage = totalAmount / distinctMonths;

                // Get category name
                var categoryName = await _dbContext.ExpenseCategories
                    .Where(c => c.Id == group.Key)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync() ?? "Unknown";

                outflows.Add(new OutflowCategoryDto
                {
                    ExpenseCategoryId = group.Key,
                    CategoryName = categoryName,
                    AverageMonthlyAmount = Math.Round(monthlyAverage, 2),
                    MonthsOfData = distinctMonths
                });
            }

            // Sort outflows by AverageMonthlyAmount descending
            outflows = outflows.OrderByDescending(o => o.AverageMonthlyAmount).ToList();

            // 4. Build daily running balance
            var totalMonthlyOutflow = outflows.Sum(o => o.AverageMonthlyAmount);
            var dailyOutflow = totalMonthlyOutflow / 30m; // Simplified even spread

            var dailyBalances = new List<DailyBalanceDto>();
            var runningBalance = startingBalance;
            DateTime? alertBreachDate = null;

            for (int day = 0; day <= daysAhead; day++)
            {
                var currentDate = today.AddDays(day);

                // Add inflows for this day
                var dayInflows = inflows.Where(i => i.AdjustedDueDate == currentDate).Sum(i => i.OutstandingAmount);
                runningBalance += dayInflows;

                // Subtract daily outflow (skip day 0 — starting balance)
                if (day > 0)
                    runningBalance -= dailyOutflow;

                dailyBalances.Add(new DailyBalanceDto
                {
                    Date = currentDate,
                    Balance = Math.Round(runningBalance, 2)
                });

                // Detect alert breach
                if (alertBreachDate == null && alertThreshold > 0 && runningBalance < alertThreshold)
                {
                    alertBreachDate = currentDate.ToDateTime(TimeOnly.MinValue);
                }
            }

            var totalInflows = inflows.Sum(i => i.OutstandingAmount);
            var totalOutflows = Math.Round(dailyOutflow * daysAhead, 2);
            var projectedBalance = dailyBalances.LastOrDefault()?.Balance ?? startingBalance;

            return new CashFlowProjectionDto
            {
                StartingBalance = startingBalance,
                AlertThreshold = alertThreshold,
                TotalInflows = totalInflows,
                TotalOutflows = totalOutflows,
                ProjectedBalance = projectedBalance,
                DailyBalances = dailyBalances,
                Inflows = inflows,
                Outflows = outflows,
                AlertBreachDate = alertBreachDate
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CashFlowSettingsDto?> GetSettingsAsync(int businessId)
    {
        try
        {
            var settings = await _dbContext.CashFlowSettings
                .FirstOrDefaultAsync(s => s.BusinessId == businessId);

            if (settings == null) return null;

            return new CashFlowSettingsDto
            {
                StartingBalance = settings.StartingBalance,
                AlertThreshold = settings.AlertThreshold,
                UpdatedAtUtc = settings.UpdatedAtUtc
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SaveSettingsAsync(int businessId, decimal startingBalance, decimal alertThreshold)
    {
        try
        {
            var settings = await _dbContext.CashFlowSettings
                .FirstOrDefaultAsync(s => s.BusinessId == businessId);

            if (settings == null)
            {
                settings = new CashFlowSettings
                {
                    BusinessId = businessId,
                    StartingBalance = startingBalance,
                    AlertThreshold = alertThreshold,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _dbContext.CashFlowSettings.Add(settings);
            }
            else
            {
                settings.StartingBalance = startingBalance;
                settings.AlertThreshold = alertThreshold;
                settings.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CashFlowWidgetDto> GetWidgetDataAsync(int businessId)
    {
        try
        {
            var settings = await _dbContext.CashFlowSettings
                .FirstOrDefaultAsync(s => s.BusinessId == businessId);

            if (settings == null)
            {
                return new CashFlowWidgetDto
                {
                    ProjectedBalance30Days = 0,
                    NetInflow = 0,
                    HasAlertBreach = false,
                    AlertBreachDate = null,
                    HasSettings = false
                };
            }

            var projection = await GetProjectionAsync(businessId, 30);

            return new CashFlowWidgetDto
            {
                ProjectedBalance30Days = projection.ProjectedBalance,
                NetInflow = projection.TotalInflows - projection.TotalOutflows,
                HasAlertBreach = projection.AlertBreachDate != null,
                AlertBreachDate = projection.AlertBreachDate,
                HasSettings = true
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
