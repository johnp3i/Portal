using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Services;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Composes the Weekly Financial Snapshot: this-week period figures (collected, expenses, net)
/// plus to-date outstanding. Owner-facing. The included figures are configurable per business
/// via <c>IncludedFiguresCsv</c>; a sensible default set is used when unset.
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
    private readonly NotificationOptions _options;

    public FinancialSnapshotComposer(
        PortalDbContext dbContext,
        IPnlService pnlService,
        IDashboardService dashboardService,
        IDigestRecipientResolver recipientResolver,
        NotificationOptions options) : base(dbContext)
    {
        _pnlService = pnlService;
        _dashboardService = dashboardService;
        _recipientResolver = recipientResolver;
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

            var selected = ParseFigures(setting?.IncludedFiguresCsv);

            var figures = new List<SnapshotFigure>();
            foreach (var key in selected)
            {
                switch (key)
                {
                    case FigCollected:
                        figures.Add(new SnapshotFigure { Key = key, Label = "Collected this week", Amount = snapshot.Revenue, Accent = "#129867" });
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

            var model = new FinancialSnapshotDigestModel
            {
                BusinessName = businessName,
                CurrencySymbol = currencySymbol,
                HasActivity = snapshot.HasData || glance.PaymentsReceived > 0 || glance.InvoicesIssued > 0,
                Figures = figures,
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
