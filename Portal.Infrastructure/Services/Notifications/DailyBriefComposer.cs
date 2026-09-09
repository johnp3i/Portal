using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Notification;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Composes the Daily Brief: an owner-facing daily summary of what needs attention, using the
/// shared <see cref="IAttentionItemBuilder"/> (same items as the Weekly Financial Snapshot).
/// Runs on a DAILY cadence via the scheduled engine. On a quiet day (no attention items) it
/// returns null so the runner sends NO email — the key difference from the weekly digests.
/// </summary>
public class DailyBriefComposer : DigestComposerBase, IDigestComposer
{
    private readonly IDigestRecipientResolver _recipientResolver;
    private readonly IAttentionItemBuilder _attentionItemBuilder;
    private readonly NotificationOptions _options;
    private readonly ILogger<DailyBriefComposer> _logger;

    public DailyBriefComposer(
        PortalDbContext dbContext,
        IDigestRecipientResolver recipientResolver,
        IAttentionItemBuilder attentionItemBuilder,
        NotificationOptions options,
        ILogger<DailyBriefComposer> logger) : base(dbContext)
    {
        _recipientResolver = recipientResolver;
        _attentionItemBuilder = attentionItemBuilder;
        _options = options;
        _logger = logger;
    }

    public string AssistantKey => DigestAssistantKeys.DailyBrief;

    public async Task<OutboxMessage?> ComposeAsync(
        int businessId, int assistantTypeId, BusinessAssistantSetting? setting, string cycleKey, CancellationToken ct)
    {
        try
        {
            var recipients = await _recipientResolver.ResolveAsync(businessId, setting);
            if (recipients == null)
            {
                // Real problem worth surfacing — distinct from a normal quiet day.
                _logger.LogWarning("Daily Brief: no resolvable recipient for BusinessId={BusinessId}.", businessId);
                return null;
            }

            var (businessName, currencySymbol) = await LoadBusinessBrandingAsync(businessId);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var items = await _attentionItemBuilder.BuildAsync(businessId, currencySymbol, today);
            if (items.Count == 0)
            {
                // Expected, normal outcome — skip today's send without any warning noise.
                _logger.LogInformation("Daily Brief: nothing to report for BusinessId={BusinessId} today — skipping.", businessId);
                return null;
            }

            var subject = DigestEmailBuilder.DailyBriefSubject(businessName);
            var body = DigestEmailBuilder.BuildDailyBriefHtml(businessName, items);

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
