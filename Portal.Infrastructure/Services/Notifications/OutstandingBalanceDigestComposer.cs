using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Services;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Composes the Weekly Outstanding Balance Digest: receivables (total outstanding, overdue,
/// top-N invoices) + upcoming supplier payments ("coming due"). Owner-facing; reuses the
/// existing dashboard/receivables query services (all tenant-less, keyed by businessId).
/// </summary>
public class OutstandingBalanceDigestComposer : DigestComposerBase, IDigestComposer
{
    private const int TopInvoices = 10;
    private const int PayablesWindowDays = 14;

    private readonly IDashboardService _dashboardService;
    private readonly IReceivablesQueryService _receivablesQueryService;
    private readonly IDigestRecipientResolver _recipientResolver;
    private readonly NotificationOptions _options;

    public OutstandingBalanceDigestComposer(
        PortalDbContext dbContext,
        IDashboardService dashboardService,
        IReceivablesQueryService receivablesQueryService,
        IDigestRecipientResolver recipientResolver,
        NotificationOptions options) : base(dbContext)
    {
        _dashboardService = dashboardService;
        _receivablesQueryService = receivablesQueryService;
        _recipientResolver = recipientResolver;
        _options = options;
    }

    public string AssistantKey => DigestAssistantKeys.WeeklyOutstandingDigest;

    public async Task<OutboxMessage?> ComposeAsync(
        int businessId, int assistantTypeId, BusinessAssistantSetting? setting, string cycleKey, CancellationToken ct)
    {
        try
        {
            // Recipient first — no point composing if we can't send.
            var recipients = await _recipientResolver.ResolveAsync(businessId, setting);
            if (recipients == null)
                return null;

            var (businessName, currencySymbol) = await LoadBusinessBrandingAsync(businessId);

            var kpi = await _dashboardService.GetKpiDataAsync(businessId);
            var receivables = await _receivablesQueryService.GetReceivablesAsync(businessId, page: 1, pageSize: TopInvoices);
            var payables = await _dashboardService.GetUpcomingSupplierPaymentsAsync(businessId, take: null, windowDays: PayablesWindowDays);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var weekFrom = today.AddDays(-6);
            var glance = await LoadWeekGlanceAsync(businessId, weekFrom, today);

            var model = new OutstandingBalanceDigestModel
            {
                BusinessName = businessName,
                CurrencySymbol = currencySymbol,
                OutstandingTotal = kpi.OutstandingReceivables,
                OutstandingInvoiceCount = kpi.OutstandingInvoiceCount,
                OverdueTotal = kpi.OverdueAmount,
                OverdueInvoiceCount = kpi.OverdueInvoiceCount,
                TopOutstanding = receivables.Items
                    .Where(r => r.OutstandingBalance > 0m)
                    .Select(r => new OutstandingInvoiceLine
                    {
                        CustomerName = r.CustomerName,
                        InvoiceNumber = r.InvoiceNumber,
                        DueDate = r.DueDate,
                        OutstandingBalance = r.OutstandingBalance
                    }).ToList(),
                UpcomingPayables = payables.Select(p => new UpcomingPayableLine
                {
                    SupplierName = p.SupplierName,
                    EffectiveDueDate = p.EffectiveDueDate,
                    TotalAmount = p.TotalAmount,
                    Status = p.Status
                }).ToList(),
                InvoicesIssuedThisWeek = glance.InvoicesIssued,
                IssuedAmountThisWeek = glance.IssuedAmount,
                PaymentsReceivedThisWeek = glance.PaymentsReceived,
                CollectedThisWeek = glance.Collected
            };

            var subject = DigestEmailBuilder.OutstandingBalanceSubject(businessName);
            var body = DigestEmailBuilder.BuildOutstandingBalanceHtml(model);

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
}
