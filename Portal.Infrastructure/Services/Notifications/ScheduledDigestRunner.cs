using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Repositories.Notification;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Per-poll orchestrator for the scheduled owner digests. For each digest assistant and each
/// plan-entitled, enabled business, decides whether the current cycle is due (per the business's
/// configured day/time in its time zone), then composes and enqueues exactly one outbox row —
/// guarded by the exact-CycleKey dedup. Tenant-less: everything is keyed by explicit businessId.
/// Resilient: a failure for one business never stops the others.
/// </summary>
public interface IScheduledDigestRunner
{
    Task RunAsync(CancellationToken ct);
}

public class ScheduledDigestRunner : IScheduledDigestRunner
{
    // Defaults when a business has not configured a schedule.
    private const byte DefaultSendDayOfWeek = 1; // Monday (0=Sunday..6=Saturday)
    private static readonly TimeOnly DefaultSendTime = new(8, 0); // 08:00 business-local

    private readonly PortalDbContext _dbContext;
    private readonly BusinessAssistantSettingRepository _settingRepository;
    private readonly NotificationOutboxRepository _outboxRepository;
    private readonly IPlanCheckService _planCheckService;
    private readonly IDigestEnqueuer _enqueuer;
    private readonly NotificationOptions _options;
    private readonly ILogger<ScheduledDigestRunner> _logger;
    private readonly Dictionary<string, IDigestComposer> _composersByKey;

    public ScheduledDigestRunner(
        PortalDbContext dbContext,
        BusinessAssistantSettingRepository settingRepository,
        NotificationOutboxRepository outboxRepository,
        IPlanCheckService planCheckService,
        IDigestEnqueuer enqueuer,
        IEnumerable<IDigestComposer> composers,
        NotificationOptions options,
        ILogger<ScheduledDigestRunner> logger)
    {
        _dbContext = dbContext;
        _settingRepository = settingRepository;
        _outboxRepository = outboxRepository;
        _planCheckService = planCheckService;
        _enqueuer = enqueuer;
        _options = options;
        _logger = logger;
        _composersByKey = composers.ToDictionary(c => c.AssistantKey, StringComparer.OrdinalIgnoreCase);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // Load the digest assistant types (id + key) once.
        var digestKeys = new[] { DigestAssistantKeys.WeeklyOutstandingDigest, DigestAssistantKeys.WeeklyFinancialSnapshot };
        var assistants = await _dbContext.AssistantTypes
            .AsNoTracking()
            .Where(a => digestKeys.Contains(a.Key))
            .Select(a => new { a.Id, a.Key })
            .ToListAsync(ct);

        if (assistants.Count == 0)
            return; // digests not seeded — nothing to do.

        // Candidate businesses: those with an active plan. Module gating is applied per business.
        var candidateBusinessIds = await _dbContext.BusinessPlans
            .AsNoTracking()
            .Where(bp => bp.IsActive)
            .Select(bp => bp.BusinessId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var businessId in candidateBusinessIds)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Plan gate (tenant-less): only businesses entitled to Digital Assistants.
                if (!await _planCheckService.IsModuleInPlanAsync(businessId, PortalModules.DigitalAssistants))
                    continue;

                var businessLocalNow = await ResolveBusinessLocalNowAsync(businessId, ct);

                foreach (var assistant in assistants)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        await ProcessBusinessAssistantAsync(businessId, assistant.Id, assistant.Key, businessLocalNow, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Digest run failed for BusinessId={BusinessId}, Assistant={AssistantKey}.",
                            businessId, assistant.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Digest run failed for BusinessId={BusinessId}.", businessId);
            }
        }
    }

    private async Task ProcessBusinessAssistantAsync(
        int businessId, int assistantTypeId, string assistantKey, DateTime businessLocalNow, CancellationToken ct)
    {
        if (!_composersByKey.TryGetValue(assistantKey, out var composer))
            return; // no composer registered for this key.

        var setting = await _settingRepository.GetAsync(businessId, assistantTypeId);

        // Absence of a row = enabled by default.
        var isEnabled = setting?.IsEnabled ?? true;
        if (!isEnabled)
            return;

        // Is the current cycle due for this business's configured day/time?
        if (!IsDue(setting, businessLocalNow))
            return;

        var cycleKey = DigestCycleKey.Weekly(assistantKey, businessLocalNow);

        // Fast pre-check (the enqueuer re-checks inside the insert to close the concurrent race).
        if (await _outboxRepository.ExistsForCycleAsync(businessId, assistantTypeId, cycleKey))
            return;

        var message = await composer.ComposeAsync(businessId, assistantTypeId, setting, cycleKey, ct);
        if (message == null)
        {
            _logger.LogWarning(
                "Digest not enqueued (no resolvable recipient) for BusinessId={BusinessId}, Assistant={AssistantKey}.",
                businessId, assistantKey);
            return;
        }

        await _enqueuer.EnqueueAsync(message);
    }

    /// <summary>
    /// Due when the business-local moment is at or past this cycle's configured send moment
    /// (day-of-week + time). No back-fill: only the current week's send moment is considered.
    /// </summary>
    private static bool IsDue(BusinessAssistantSetting? setting, DateTime businessLocalNow)
    {
        var sendDay = setting?.SendDayOfWeek ?? DefaultSendDayOfWeek;   // 0=Sunday..6=Saturday
        var sendTime = setting?.SendTimeLocal ?? DefaultSendTime;

        // .NET DayOfWeek: Sunday=0..Saturday=6 — matches our stored convention.
        var currentDow = (int)businessLocalNow.DayOfWeek;

        // The send moment for the CURRENT week (this week's occurrence of the configured day).
        var sendMomentThisWeek = businessLocalNow.Date
            .AddDays(sendDay - currentDow)
            .Add(sendTime.ToTimeSpan());

        // Due if we've reached this week's send moment. (Dedup ensures at-most-once per cycle.)
        return businessLocalNow >= sendMomentThisWeek;
    }

    private async Task<DateTime> ResolveBusinessLocalNowAsync(int businessId, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;

        var timeZoneId = await _dbContext.Businesses
            .AsNoTracking()
            .Where(b => b.Id == businessId)
            .Select(b => b.TimeZoneId)
            .FirstOrDefaultAsync(ct);

        string? windowsId = null;
        if (timeZoneId != null)
        {
            windowsId = await _dbContext.NotificationTimeZones
                .AsNoTracking()
                .Where(tz => tz.Id == timeZoneId)
                .Select(tz => tz.WindowsId)
                .FirstOrDefaultAsync(ct);
        }

        windowsId ??= _options.DefaultTimeZoneWindowsId;

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            return TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Unknown time zone '{WindowsId}' for BusinessId={BusinessId}; falling back to UTC.",
                windowsId, businessId);
            return nowUtc;
        }
    }
}
