using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Services;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Composes the Weekly Financial Snapshot: this-week period figures (collected, expenses, net)
/// plus to-date outstanding, and an employee-report "needs your attention" section (overdue,
/// oldest overdue invoice, supplier payments due, week-over-week collected, quotations awaiting
/// response, VAT deadline). Owner-facing. Included figures are configurable per business via
/// <c>IncludedFiguresCsv</c>; a sensible default set is used when unset. Fully tenant-less.
/// </summary>
public class FinancialSnapshotComposer : DigestComposerBase, IDigestComposer
{
    // Figure keys (stable identifiers used in IncludedFiguresCsv).
    public const string FigCollected = "collected";
    public const string FigExpenses = "expenses";
    public const string FigNet = "net";
    public const string FigOutstanding = "outstanding";
    public const string FigOverdue = "overdue";

    private static readonly string[] DefaultFigures = { FigCollected, FigExpenses, FigNet, FigOutstanding };

    private readonly IPnlService _pnlService;
    private readonly IDashboardService _dashboardService;
    private readonly IDigestRecipientResolver _recipientResolver;
    private readonly IAttentionItemBuilder _attentionItemBuilder;
    private readonly NotificationOptions _options;

    public FinancialSnapshotComposer(
        PortalDbContext dbContext,
        IPnlService pnlService,
        IDashboardService dashboardService,
        IDigestRecipientResolver recipientResolver,
        IAttentionItemBuilder attentionItemBuilder,
        NotificationOptions options) : base(dbContext)
    {
        _pnlService = pnlService;
        _dashboardService = dashboardService;
        _recipientResolver = recipientResolver;
        _attentionItemBuilder = attentionItemBuilder;
        _options = options;
    }

    public string AssistantKey => DigestAssistantKeys.WeeklyFinancialSnapshot;

    public async Task<OutboxMessage?> ComposeAsync(
        int businessId, int assistantTypeId, BusinessAssistantSetting? setting, string cycleKey, CancellationToken ct)
    {
        try
        {
            var recipients = await _recipientResolver.ResolveAsync(businessId, setting);
            if (recipients == null)
                return null;

            var (businessName, currencySymbol) = await LoadBusinessBrandingAsync(businessId);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var weekFrom = today.AddDays(-6);

            var snapshot = await _pnlService.ComputeSnapshotAsync(businessId, weekFrom, today);
            var kpi = await _dashboardService.GetKpiDataAsync(businessId);
            var glance = await LoadWeekGlanceAsync(businessId, weekFrom, today);

            // Prior week (for week-over-week collected trend).
            var priorTo = weekFrom.AddDays(-1);
            var priorFrom = priorTo.AddDays(-6);
            var priorSnapshot = await _pnlService.ComputeSnapshotAsync(businessId, priorFrom, priorTo);

            var selected = ParseFigures(setting?.IncludedFiguresCsv);

            var figures = new List<SnapshotFigure>();
            foreach (var key in selected)
            {
                switch (key)
                {
                    case FigCollected:
                        figures.Add(new SnapshotFigure
                        {
                            Key = key,
                            Label = "Collected this week",
                            Amount = snapshot.Revenue,
                            Accent = "#129867",
                            TrendNote = BuildCollectedTrendNote(currencySymbol, snapshot.Revenue, priorSnapshot.Revenue)
                        });
                        break;
                    case FigExpenses:
                        figures.Add(new SnapshotFigure { Key = key, Label = "Expenses this week", Amount = snapshot.Cogs + snapshot.OperatingExpenses });
                        break;
                    case FigNet:
                        figures.Add(new SnapshotFigure { Key = key, Label = "Net this week", Amount = snapshot.NetProfit, Accent = snapshot.NetProfit < 0 ? "#C24A4A" : "#0B1B28" });
                        break;
                    case FigOutstanding:
                        figures.Add(new SnapshotFigure { Key = key, Label = "Total outstanding", Amount = kpi.OutstandingReceivables, Detail = $"{kpi.OutstandingInvoiceCount} invoice(s)" });
                        break;
                    case FigOverdue:
                        figures.Add(new SnapshotFigure { Key = key, Label = "Overdue", Amount = kpi.OverdueAmount, Detail = $"{kpi.OverdueInvoiceCount} invoice(s)", Accent = "#C24A4A" });
                        break;
                }
            }

            // Pass the already-loaded KPI so the shared builder doesn't re-query it.
            var attention = await _attentionItemBuilder.BuildAsync(businessId, currencySymbol, today, kpi);

            var model = new FinancialSnapshotDigestModel
            {
                BusinessName = businessName,
                CurrencySymbol = currencySymbol,
                HasActivity = snapshot.HasData || glance.PaymentsReceived > 0 || glance.InvoicesIssued > 0,
                Figures = figures,
                AttentionItems = attention,
                InvoicesIssuedThisWeek = glance.InvoicesIssued,
                IssuedAmountThisWeek = glance.IssuedAmount,
                PaymentsReceivedThisWeek = glance.PaymentsReceived,
                CollectedThisWeek = glance.Collected
            };

            var subject = DigestEmailBuilder.FinancialSnapshotSubject(businessName);
            var body = DigestEmailBuilder.BuildFinancialSnapshotHtml(model);

            return new OutboxMessage
            {
                BusinessId = businessId,
                AssistantTypeId = assistantTypeId,
                RecipientEmail = recipients.PrimaryEmail,
                RecipientName = null,
                ReplyToEmail = null,
                Subject = subject,
                BodyHtml = body,
                OutboxMessageStatusTypeId = OutboxMessageStatusTypes.Pending,
                RetryCount = 0,
                MaxRetries = _options.DefaultMaxRetries,
                ScheduledForUtc = DateTime.UtcNow,
                CycleKey = cycleKey,
                CreatedAtUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static string BuildCollectedTrendNote(string cur, decimal thisWeek, decimal lastWeek)
    {
        var arrow = thisWeek > lastWeek ? "▲" : thisWeek < lastWeek ? "▼" : "▬";
        return $"{arrow} vs {cur}{lastWeek:N2} last week";
    }

    private static List<string> ParseFigures(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return DefaultFigures.ToList();

        var known = new[] { FigCollected, FigExpenses, FigNet, FigOutstanding, FigOverdue };
        var selected = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => known.Contains(k, StringComparer.OrdinalIgnoreCase))
            .Select(k => k.ToLowerInvariant())
            .Distinct()
            .ToList();

        return selected.Count > 0 ? selected : DefaultFigures.ToList();
    }
}
