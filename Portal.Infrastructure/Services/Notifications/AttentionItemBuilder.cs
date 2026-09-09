using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Builds the shared "needs your attention" employee-report lines used by both the Weekly
/// Financial Snapshot and the Daily Brief. One source of truth so the two emails never drift.
/// Tenant-less (explicit businessId). Each line is included only when it has something to report,
/// so a healthy business yields a short (or empty) list.
/// </summary>
public interface IAttentionItemBuilder
{
    /// <summary>
    /// Builds the attention items for a business as of <paramref name="today"/> (business-local).
    /// <paramref name="kpi"/> is optional: pass an already-loaded <see cref="DashboardKpiDto"/>
    /// to avoid a duplicate query (the Snapshot does this); pass null to have the builder fetch it.
    /// </summary>
    Task<List<AttentionItem>> BuildAsync(
        int businessId, string currencySymbol, DateOnly today, DashboardKpiDto? kpi = null);
}

public class AttentionItemBuilder : IAttentionItemBuilder
{
    private const int PayablesWindowDays = 7; // "due this week" for the attention line

    private readonly IDashboardService _dashboardService;
    private readonly QuotationRepository _quotationRepository;
    private readonly VatSubmissionPeriodRepository _vatPeriodRepository;
    private readonly NotificationOptions _options;

    public AttentionItemBuilder(
        IDashboardService dashboardService,
        QuotationRepository quotationRepository,
        VatSubmissionPeriodRepository vatPeriodRepository,
        NotificationOptions options)
    {
        _dashboardService = dashboardService;
        _quotationRepository = quotationRepository;
        _vatPeriodRepository = vatPeriodRepository;
        _options = options;
    }

    public async Task<List<AttentionItem>> BuildAsync(
        int businessId, string cur, DateOnly today, DashboardKpiDto? kpi = null)
    {
        // Reuse the caller's KPI when supplied (avoids a second query); else fetch it.
        kpi ??= await _dashboardService.GetKpiDataAsync(businessId);

        var items = new List<AttentionItem>();

        // 1. Overdue receivables.
        if (kpi.OverdueInvoiceCount > 0)
        {
            items.Add(new AttentionItem
            {
                Text = $"{kpi.OverdueInvoiceCount} invoice(s) overdue totalling {cur}{kpi.OverdueAmount:N2}.",
                IsUrgent = true
            });

            // 2. Oldest overdue invoice — one concrete thing to chase.
            var oldest = await _dashboardService.GetOldestOverdueInvoiceAsync(businessId);
            if (oldest != null)
            {
                items.Add(new AttentionItem
                {
                    Text = $"Oldest unpaid: {oldest.InvoiceNumber} — {cur}{oldest.Outstanding:N2}, {oldest.DaysOverdue} day(s) overdue.",
                    IsUrgent = true
                });
            }
        }

        // 3. Supplier payments due this week (money going out).
        var payables = await _dashboardService.GetUpcomingSupplierPaymentsAsync(businessId, take: null, windowDays: PayablesWindowDays);
        if (payables.Count > 0)
        {
            var total = payables.Sum(p => p.TotalAmount);
            var overdueOut = payables.Count(p => p.Status == "overdue");
            var note = overdueOut > 0 ? $" ({overdueOut} already overdue)" : string.Empty;
            items.Add(new AttentionItem
            {
                Text = $"{payables.Count} supplier payment(s) coming due this week, totalling {cur}{total:N2}{note}.",
                IsUrgent = overdueOut > 0
            });
        }

        // 4. Quotations awaiting a customer response.
        var awaiting = await _quotationRepository.CountAwaitingResponseAsync(businessId, today);
        if (awaiting > 0)
        {
            items.Add(new AttentionItem
            {
                Text = $"{awaiting} quotation(s) sent and still awaiting a response — worth a follow-up."
            });
        }

        // 5. VAT filing deadline for the current unsubmitted period (derived; no stored due date).
        var period = await _vatPeriodRepository.GetCoveringUnsubmittedPeriodAsync(businessId, today);
        if (period != null)
        {
            var deadline = period.PeriodEndDate.AddDays(_options.VatFilingOffsetDays);
            var daysUntil = deadline.DayNumber - today.DayNumber;
            if (daysUntil >= 0 && daysUntil <= _options.VatDeadlineNoticeDays)
            {
                var whenText = daysUntil == 0 ? "today" : $"in {daysUntil} day(s)";
                items.Add(new AttentionItem
                {
                    Text = $"VAT return for {period.PeriodLabel ?? period.PeriodEndDate.ToString("MMM yyyy")} is due {whenText} ({deadline:dd MMM yyyy}).",
                    IsUrgent = daysUntil <= 7
                });
            }
        }

        return items;
    }
}
