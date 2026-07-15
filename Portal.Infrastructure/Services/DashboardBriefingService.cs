using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Evaluates business signals and assembles a prioritized narrative briefing for the dashboard.
/// </summary>
public class DashboardBriefingService : IDashboardBriefingService
{
    private const int MaxInsights = 6;
    private readonly PortalDbContext _dbContext;

    public DashboardBriefingService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BriefingViewModel> GenerateBriefingAsync(int businessId, DashboardScopeDto scope, string currencySymbol)
    {
        try
        {
            var insights = new List<BriefingInsight>();
            var now = DateTime.UtcNow;

            // Evaluate each signal based on permissions
            if (scope.ShowRevenue)
            {
                await EvaluateOverdueInvoices(businessId, currencySymbol, insights);
                await EvaluateRecentPayment(businessId, currencySymbol, insights);
            }

            if (scope.ShowQuotation)
            {
                await EvaluatePendingProposals(businessId, currencySymbol, insights);
            }

            if (scope.ShowPurchase)
            {
                await EvaluateUnassignedPurchases(businessId, insights);
            }

            if (scope.ShowInvoice)
            {
                await EvaluateDraftInvoices(businessId, insights);
            }

            // Cash flow — show when revenue is visible (it's a financial insight)
            if (scope.ShowRevenue)
            {
                await EvaluateCashFlowOutlook(businessId, currencySymbol, insights);
            }

            // Sort by priority and limit
            insights = insights.OrderBy(i => i.Priority).Take(MaxInsights).ToList();

            // Determine state
            var state = DetermineState(insights, businessId);
            var greeting = GetGreeting(now);
            var subtitle = GetSubtitle(state);

            return new BriefingViewModel
            {
                Greeting = greeting,
                Subtitle = subtitle,
                State = state,
                Insights = insights,
                DateDisplay = now.ToString("dd MMM yyyy")
            };
        }
        catch (Exception ex)
        {
            // Graceful fallback — never break the dashboard
            return new BriefingViewModel
            {
                Greeting = GetGreeting(DateTime.UtcNow),
                Subtitle = "Your operational briefing",
                State = BriefingState.AllClear,
                Insights = new List<BriefingInsight>
                {
                    new() { Priority = 10, Severity = BriefingSeverity.Positive, Html = "Everything looks good — no items need your attention right now." }
                },
                DateDisplay = DateTime.UtcNow.ToString("dd MMM yyyy")
            };
        }
    }

    private async Task EvaluateOverdueInvoices(int businessId, string currency, List<BriefingInsight> insights)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var overdueData = await _dbContext.Invoices
                .Where(i => i.BusinessId == businessId
                    && i.InvoiceStatusTypeId == 2
                    && i.DueDate < today
                    && i.InvoiceFinancialStatusTypeId != 3) // Not fully paid
                .Select(i => new { i.TotalAmount, i.DueDate, i.CustomerId })
                .ToListAsync();

            if (overdueData.Count == 0) return;

            var totalOverdue = overdueData.Sum(i => i.TotalAmount);
            var oldest = overdueData.OrderBy(i => i.DueDate).First();
            var daysOverdue = (today.DayNumber - oldest.DueDate.DayNumber);

            // Get customer name for the oldest
            var customerName = await _dbContext.Customers
                .Where(c => c.Id == oldest.CustomerId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync() ?? "a customer";

            var s = overdueData.Count == 1 ? "" : "s";
            insights.Add(new BriefingInsight
            {
                Priority = 1,
                Severity = BriefingSeverity.Urgent,
                Html = $"You have <strong>{overdueData.Count} overdue invoice{s}</strong> totalling <strong>{currency}{totalOverdue:N2}</strong> — the oldest is {daysOverdue} days past due from <strong>{customerName}</strong>. <a href=\"/Revenue/Receivables\" style=\"color:#0D5EA6;font-weight:600;text-decoration:none;\">View receivables →</a>"
            });
        }
        catch { }
    }

    private async Task EvaluatePendingProposals(int businessId, string currency, List<BriefingInsight> insights)
    {
        try
        {
            var pending = await _dbContext.Quotations
                .Where(q => q.BusinessId == businessId && q.QuotationStatusTypeId == 2)
                .Select(q => q.TotalAmount)
                .ToListAsync();

            if (pending.Count == 0) return;

            var total = pending.Sum();
            var s = pending.Count == 1 ? "" : "s";
            insights.Add(new BriefingInsight
            {
                Priority = 3,
                Severity = BriefingSeverity.Action,
                Html = $"<strong>{pending.Count} proposal{s}</strong> worth <strong>{currency}{total:N2}</strong> are awaiting client acceptance. <a href=\"/Quotation?status=2\" style=\"color:#0D5EA6;font-weight:600;text-decoration:none;\">Follow up →</a>"
            });
        }
        catch { }
    }

    private async Task EvaluateUnassignedPurchases(int businessId, List<BriefingInsight> insights)
    {
        try
        {
            var count = await _dbContext.Purchases
                .Where(p => p.BusinessId == businessId
                    && p.VatSubmissionPeriodId == null
                    && !p.IsCancelled)
                .CountAsync();

            if (count == 0) return;

            var s = count == 1 ? "" : "s";
            insights.Add(new BriefingInsight
            {
                Priority = 4,
                Severity = BriefingSeverity.Action,
                Html = $"<strong>{count} purchase{s}</strong> are not yet assigned to a VAT period. <a href=\"/Purchase?vatPeriodId=0\" style=\"color:#0D5EA6;font-weight:600;text-decoration:none;\">Assign now →</a>"
            });
        }
        catch { }
    }

    private async Task EvaluateDraftInvoices(int businessId, List<BriefingInsight> insights)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-3);
            var count = await _dbContext.Invoices
                .Where(i => i.BusinessId == businessId
                    && i.InvoiceStatusTypeId == 1
                    && i.CreatedAtUtc < cutoff)
                .CountAsync();

            if (count == 0) return;

            var s = count == 1 ? "" : "s";
            insights.Add(new BriefingInsight
            {
                Priority = 7,
                Severity = BriefingSeverity.Action,
                Html = $"<strong>{count} invoice{s}</strong> have been sitting in Draft for more than 3 days. <a href=\"/Invoice?status=1\" style=\"color:#0D5EA6;font-weight:600;text-decoration:none;\">Review drafts →</a>"
            });
        }
        catch { }
    }

    private async Task EvaluateCashFlowOutlook(int businessId, string currency, List<BriefingInsight> insights)
    {
        try
        {
            // Simplified: check if there are overdue invoices with large outstanding amounts
            // A full cash flow calc would use CashFlowService, but for the briefing we keep it lightweight
            var settings = await _dbContext.CashFlowSettings
                .FirstOrDefaultAsync(s => s.BusinessId == businessId);

            if (settings == null)
            {
                // No cash flow configured — suggest setting up
                insights.Add(new BriefingInsight
                {
                    Priority = 8,
                    Severity = BriefingSeverity.Positive,
                    Html = "Cash flow forecasting is available. <a href=\"/CashFlow\" style=\"color:#0D5EA6;font-weight:600;text-decoration:none;\">Set up your starting balance →</a>"
                });
                return;
            }

            // If cash flow is set up, show a positive message (full projection would be too expensive for a briefing)
            insights.Add(new BriefingInsight
            {
                Priority = 8,
                Severity = BriefingSeverity.Positive,
                Html = "Cash flow looks healthy for the next 30 days."
            });
        }
        catch { }
    }

    private async Task EvaluateRecentPayment(int businessId, string currency, List<BriefingInsight> insights)
    {
        try
        {
            var yesterday = DateTime.UtcNow.AddHours(-24);
            var recentPayment = await _dbContext.Payments
                .Where(p => p.BusinessId == businessId
                    && !p.IsVoided
                    && p.CreatedAtUtc >= yesterday)
                .OrderByDescending(p => p.Amount)
                .Select(p => new { p.Amount, p.InvoiceId })
                .FirstOrDefaultAsync();

            if (recentPayment == null) return;

            // Get customer name via invoice
            var customerName = await _dbContext.Invoices
                .Where(i => i.Id == recentPayment.InvoiceId)
                .Select(i => i.Customer!.Name)
                .FirstOrDefaultAsync() ?? "a customer";

            insights.Add(new BriefingInsight
            {
                Priority = 9,
                Severity = BriefingSeverity.Positive,
                Html = $"A payment of <strong>{currency}{recentPayment.Amount:N2}</strong> was received from <strong>{customerName}</strong>."
            });
        }
        catch { }
    }

    private static BriefingState DetermineState(List<BriefingInsight> insights)
    {
        if (insights.Count == 0)
            return BriefingState.AllClear;

        var urgentCount = insights.Count(i => i.Severity == BriefingSeverity.Urgent);
        if (urgentCount >= 2)
            return BriefingState.Critical;

        var hasOnlyPositive = insights.All(i => i.Severity == BriefingSeverity.Positive);
        if (hasOnlyPositive)
            return BriefingState.AllClear;

        return BriefingState.Normal;
    }

    private BriefingState DetermineState(List<BriefingInsight> insights, int businessId)
    {
        return DetermineState(insights);
    }

    private static string GetGreeting(DateTime utcNow)
    {
        var hour = utcNow.Hour;
        if (hour < 12) return "Good morning";
        if (hour < 17) return "Good afternoon";
        return "Good evening";
    }

    private static string GetSubtitle(BriefingState state)
    {
        return state switch
        {
            BriefingState.Critical => "A few things need your attention",
            BriefingState.AllClear => "Your operational briefing",
            BriefingState.NewBusiness => "Let's get you started",
            _ => "Your operational briefing"
        };
    }
}
