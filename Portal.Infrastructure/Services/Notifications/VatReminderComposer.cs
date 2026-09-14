using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Repositories.Notification;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Composes the VAT Period Due Reminder: an owner-facing reminder, sent ONCE per VAT period as its
/// derived filing deadline approaches, including the approximate net VAT payable. Runs on a DAILY
/// cadence via the scheduled engine but de-dupes per PERIOD (period-scoped cycle key), so the owner
/// is reminded once per period, not once per day across the notice window. Returns null to skip
/// (nothing due today, or every in-window period already reminded).
/// </summary>
public class VatReminderComposer : DigestComposerBase, IDigestComposer
{
    private readonly IDigestRecipientResolver _recipientResolver;
    private readonly VatSubmissionPeriodRepository _vatPeriodRepository;
    private readonly IVatSubmissionService _vatSubmissionService;
    private readonly NotificationOutboxRepository _outboxRepository;
    private readonly NotificationOptions _options;
    private readonly ILogger<VatReminderComposer> _logger;

    public VatReminderComposer(
        PortalDbContext dbContext,
        IDigestRecipientResolver recipientResolver,
        VatSubmissionPeriodRepository vatPeriodRepository,
        IVatSubmissionService vatSubmissionService,
        NotificationOutboxRepository outboxRepository,
        NotificationOptions options,
        ILogger<VatReminderComposer> logger) : base(dbContext)
    {
        _recipientResolver = recipientResolver;
        _vatPeriodRepository = vatPeriodRepository;
        _vatSubmissionService = vatSubmissionService;
        _outboxRepository = outboxRepository;
        _options = options;
        _logger = logger;
    }

    public string AssistantKey => DigestAssistantKeys.VatPeriodDueReminder;

    public async Task<OutboxMessage?> ComposeAsync(
        int businessId, int assistantTypeId, BusinessAssistantSetting? setting, string cycleKey, CancellationToken ct)
    {
        try
        {
            // Deadline-day math uses the UTC date (whole-day granularity); send time-of-day is gated
            // by the runner's timezone-aware IsDue, so the cycleKey argument (a daily date key) is not
            // used here — the period-scoped key is authoritative.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var leadDays = setting?.VatNoticeLeadDays ?? _options.VatDeadlineNoticeDays;

            // Scan unsubmitted periods. Start the scan well before today so a period whose window has
            // ended but whose derived deadline is still imminent is still returned (the deadline is up
            // to VatFilingOffsetDays after PeriodEndDate, and periods can be up to a year long).
            var earlyBound = today.AddDays(-(_options.VatFilingOffsetDays + 366));
            var periods = await _vatPeriodRepository.GetUnsubmittedPeriodsFromAsync(businessId, earlyBound);

            // Most-urgent-first, in the notice window, that has NOT already been reminded. Picking the
            // first not-yet-reminded period (rather than always the earliest) prevents a reminded
            // period from perpetually starving a second in-window period.
            var inWindow = periods
                .Select(p => new { Period = p, Deadline = VatDeadline.For(p, _options) })
                .Where(x =>
                {
                    var d = x.Deadline.DayNumber - today.DayNumber;
                    return d >= 0 && d <= leadDays;
                })
                .OrderBy(x => x.Deadline)
                .ToList();

            foreach (var candidate in inWindow)
            {
                var periodCycleKey = DigestCycleKey.Period(AssistantKey, candidate.Period.Id);
                if (await _outboxRepository.ExistsForCycleAsync(businessId, assistantTypeId, periodCycleKey))
                    continue; // already reminded for this period — try the next in-window period

                // Recipient — resolve only once we know there is something to send.
                var recipients = await _recipientResolver.ResolveAsync(businessId, setting);
                if (recipients == null)
                {
                    _logger.LogWarning("VAT reminder: no resolvable recipient for BusinessId={BusinessId}.", businessId);
                    return null;
                }

                var (businessName, currencySymbol) = await LoadBusinessBrandingAsync(businessId);
                var netVat = await _vatSubmissionService.GetApproxNetVatPayableAsync(businessId, candidate.Period);

                var deadline = candidate.Deadline;
                var daysUntil = deadline.DayNumber - today.DayNumber;
                var periodLabel = string.IsNullOrWhiteSpace(candidate.Period.PeriodLabel)
                    ? candidate.Period.PeriodEndDate.ToString("MMM yyyy")
                    : candidate.Period.PeriodLabel;

                var subject = DigestEmailBuilder.VatReminderSubject(businessName, periodLabel);
                var body = DigestEmailBuilder.BuildVatReminderHtml(
                    businessName, currencySymbol, periodLabel, deadline, daysUntil, netVat);

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
                    CycleKey = periodCycleKey,
                    CreatedAtUtc = DateTime.UtcNow
                };
            }

            // Nothing due today, or every in-window period already reminded — expected quiet skip.
            _logger.LogInformation(
                "VAT reminder: no un-reminded unsubmitted period within the notice window for BusinessId={BusinessId} — skipping.",
                businessId);
            return null;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
