using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Repositories.Notification;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Default producer. All gating and resolution happen in <see cref="PrepareThankYouAsync"/>
/// (read-only, no transaction); the caller then inserts the prepared row inside its own
/// transaction via <see cref="InsertAsync"/>.
/// </summary>
public class NotificationProducer : INotificationProducer
{
    public const string ThankYouKey = "thank_you";
    private const string RelatedEntityInvoice = "Invoice";

    private readonly PortalDbContext _dbContext;
    private readonly NotificationOutboxRepository _outboxRepository;
    private readonly BusinessAssistantSettingRepository _settingRepository;
    private readonly AssistantOptOutRepository _optOutRepository;
    private readonly IScheduleResolver _scheduleResolver;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationProducer> _logger;

    public NotificationProducer(
        PortalDbContext dbContext,
        NotificationOutboxRepository outboxRepository,
        BusinessAssistantSettingRepository settingRepository,
        AssistantOptOutRepository optOutRepository,
        IScheduleResolver scheduleResolver,
        NotificationOptions options,
        ILogger<NotificationProducer> logger)
    {
        _dbContext = dbContext;
        _outboxRepository = outboxRepository;
        _settingRepository = settingRepository;
        _optOutRepository = optOutRepository;
        _scheduleResolver = scheduleResolver;
        _options = options;
        _logger = logger;
    }

    public async Task<OutboxMessage?> PrepareThankYouAsync(
        int businessId,
        int invoiceId,
        string customerName,
        string customerEmail,
        decimal amount,
        string invoiceNumber,
        string? replyToEmail)
    {
        try
        {
            // No recipient → nothing to send.
            if (string.IsNullOrWhiteSpace(customerEmail))
                return null;

            // Resolve the assistant type (seeded).
            var assistant = await _dbContext.AssistantTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Key == ThankYouKey);
            if (assistant == null)
                return null; // assistant not seeded — fail safe, no send

            // Gating: per-business enabled? (absence of a row = default enabled)
            var setting = await _settingRepository.GetAsync(businessId, assistant.Id);
            var isEnabled = setting?.IsEnabled ?? true;
            if (!isEnabled)
                return null;

            // Gating: recipient opted out of THIS assistant?
            if (await _optOutRepository.ExistsAsync(businessId, assistant.Id, customerEmail))
                return null;

            // Dedup: a Thank-You for this invoice is already queued/sent? (guards double-submits
            // and retried Stripe webhooks). Failed messages do not block a genuine re-enqueue.
            if (await _outboxRepository.ExistsForRelatedEntityAsync(businessId, assistant.Id, RelatedEntityInvoice, invoiceId))
                return null;

            var includeFooter = setting?.IsBrandingFooterEnabled ?? true;

            // Resolve currency symbol + business name + timezone.
            var profile = await _dbContext.BusinessProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);
            var currencySymbol = profile?.CurrencySymbol ?? "€";

            var business = await _dbContext.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == businessId);
            var businessName = business?.Name ?? "Your supplier";

            string? windowsTimeZoneId = null;
            if (business?.TimeZoneId != null)
            {
                windowsTimeZoneId = await _dbContext.NotificationTimeZones
                    .AsNoTracking()
                    .Where(tz => tz.Id == business.TimeZoneId)
                    .Select(tz => tz.WindowsId)
                    .FirstOrDefaultAsync();
            }

            // Working-hours → ScheduledForUtc.
            var scheduledForUtc = _scheduleResolver.ResolveScheduledForUtc(
                setting?.WorkingHoursStart,
                setting?.WorkingHoursEnd,
                windowsTimeZoneId,
                DateTime.UtcNow);

            // Render subject + body at write time (self-contained).
            var subject = AssistantEmailBuilder.ThankYouSubject(invoiceNumber);
            var body = AssistantEmailBuilder.BuildThankYouHtml(
                customerName, amount, invoiceNumber, businessName, currencySymbol,
                includeFooter, _options.BrandingFooterUrl);

            return new OutboxMessage
            {
                BusinessId = businessId,
                AssistantTypeId = assistant.Id,
                RecipientEmail = customerEmail,
                RecipientName = customerName,
                ReplyToEmail = replyToEmail,
                Subject = subject,
                BodyHtml = body,
                OutboxMessageStatusTypeId = Portal.Infrastructure.Constants.OutboxMessageStatusTypes.Pending,
                RetryCount = 0,
                MaxRetries = _options.DefaultMaxRetries,
                ScheduledForUtc = scheduledForUtc,
                RelatedEntityType = RelatedEntityInvoice,
                RelatedEntityId = invoiceId,
                CreatedAtUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task InsertAsync(OutboxMessage message)
    {
        try
        {
            // Final dedup guard, evaluated inside the caller's transaction. The Prepare step
            // already checked against committed state, but two truly-concurrent payments for the
            // same invoice could both pass Prepare before either commits. This re-check closes
            // that race and logs the (extremely rare) collision instead of enqueuing a duplicate.
            if (message.RelatedEntityId.HasValue && !string.IsNullOrEmpty(message.RelatedEntityType))
            {
                var alreadyExists = await _outboxRepository.ExistsForRelatedEntityAsync(
                    message.BusinessId, message.AssistantTypeId, message.RelatedEntityType, message.RelatedEntityId.Value);
                if (alreadyExists)
                {
                    _logger.LogWarning(
                        "Notification enqueue collision detected — a non-failed message already exists for " +
                        "BusinessId={BusinessId}, AssistantTypeId={AssistantTypeId}, {EntityType}={EntityId}. Skipping duplicate.",
                        message.BusinessId, message.AssistantTypeId, message.RelatedEntityType, message.RelatedEntityId.Value);
                    return;
                }
            }

            await _outboxRepository.InsertAsync(message);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
