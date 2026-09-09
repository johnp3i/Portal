using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Repositories.Notification;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Notifications;
using Portal.Web.Models.Assistants;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.DigitalAssistants)]
public class AssistantsController : Controller
{
    private readonly PortalDbContext _dbContext;
    private readonly ICurrentTenantService _tenantService;
    private readonly BusinessAssistantSettingRepository _settingRepository;
    private readonly NotificationOutboxRepository _outboxRepository;

    public AssistantsController(
        PortalDbContext dbContext,
        ICurrentTenantService tenantService,
        BusinessAssistantSettingRepository settingRepository,
        NotificationOutboxRepository outboxRepository)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
        _settingRepository = settingRepository;
        _outboxRepository = outboxRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var assistants = await _dbContext.AssistantTypes
                .AsNoTracking()
                .OrderBy(a => a.Id)
                .ToListAsync();

            var settings = await _settingRepository.GetAllForBusinessAsync(businessId);
            var settingsByType = settings.ToDictionary(s => s.AssistantTypeId);

            var model = new AssistantsIndexViewModel
            {
                Assistants = assistants.Select(a =>
                {
                    settingsByType.TryGetValue(a.Id, out var s);
                    // Classify the card shape:
                    //  - customer-facing  → working hours + footer (e.g. Thank-You)
                    //  - daily brief      → send-time only + recipient (no day-of-week, no figures)
                    //  - event alert      → recipient only, no schedule (e.g. New Payment Received)
                    //  - weekly digest    → day + time + recipient (+ figures for the snapshot)
                    var isDailyBrief = a.Key == DigestAssistantKeys.DailyBrief;
                    var isEventAlert = !a.IsCustomerFacing && a.Key == NotificationProducer.NewPaymentKey;
                    // Scheduled = owner-facing on a clock (weekly digests OR the daily brief), but NOT the event alert.
                    var isScheduled = !a.IsCustomerFacing && !isEventAlert;
                    return new AssistantCardViewModel
                    {
                        AssistantTypeId = a.Id,
                        Key = a.Key,
                        Name = a.Name,
                        Description = a.Description,
                        IsCustomerFacing = a.IsCustomerFacing,
                        // Defaults when no row exists: enabled, footer on, no working-hours restriction.
                        IsEnabled = s?.IsEnabled ?? true,
                        IsBrandingFooterEnabled = s?.IsBrandingFooterEnabled ?? true,
                        WorkingHoursStart = s?.WorkingHoursStart?.ToString("HH:mm"),
                        WorkingHoursEnd = s?.WorkingHoursEnd?.ToString("HH:mm"),
                        // Digest fields (defaults: Monday 08:00, owner recipient).
                        IsScheduledDigest = isScheduled,
                        IsDailyBrief = isDailyBrief,
                        IsEventAlert = isEventAlert,
                        SendDayOfWeek = s?.SendDayOfWeek ?? 1,
                        SendTimeLocal = s?.SendTimeLocal?.ToString("HH:mm") ?? "08:00",
                        RecipientOverride = s?.RecipientOverride,
                        IsRecipientOwnerIncluded = s?.IsRecipientOwnerIncluded ?? true,
                        HasFigureSelection = a.Key == DigestAssistantKeys.WeeklyFinancialSnapshot,
                        IncludedFigures = string.IsNullOrWhiteSpace(s?.IncludedFiguresCsv)
                            ? new List<string>()
                            : s!.IncludedFiguresCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                    };
                }).ToList()
            };

            return View(model);
        }
        catch (Exception ex)
        {
            return View(new AssistantsIndexViewModel());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleAssistant([FromBody] ToggleAssistantRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var assistant = await _dbContext.AssistantTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Key == request.AssistantKey);
            if (assistant == null)
                return Json(new { success = false, message = "Assistant not found." });

            var existing = await _settingRepository.GetAsync(businessId, assistant.Id);

            var setting = new BusinessAssistantSetting
            {
                BusinessId = businessId,
                AssistantTypeId = assistant.Id,
                IsEnabled = request.Enabled,
                // Preserve ALL other settings (or apply defaults if this is the first write) —
                // the upsert writes every column, so a toggle must carry these forward or it
                // would NULL a configured digest schedule/recipient/figures (data-loss guard).
                IsBrandingFooterEnabled = existing?.IsBrandingFooterEnabled ?? true,
                WorkingHoursStart = existing?.WorkingHoursStart,
                WorkingHoursEnd = existing?.WorkingHoursEnd,
                SendDayOfWeek = existing?.SendDayOfWeek,
                SendTimeLocal = existing?.SendTimeLocal,
                RecipientOverride = existing?.RecipientOverride,
                IsRecipientOwnerIncluded = existing?.IsRecipientOwnerIncluded ?? true,
                IncludedFiguresCsv = existing?.IncludedFiguresCsv
            };

            await _settingRepository.UpsertAsync(setting);

            return Json(new { success = true, message = request.Enabled ? "Assistant enabled." : "Assistant disabled." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSaveAssistantSettings([FromBody] SaveAssistantSettingsRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var assistant = await _dbContext.AssistantTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Key == request.AssistantKey);
            if (assistant == null)
                return Json(new { success = false, message = "Assistant not found." });

            var existing = await _settingRepository.GetAsync(businessId, assistant.Id);

            // Start from existing (or defaults) and preserve everything not being changed — the
            // upsert writes all columns, so partial writes must carry the rest forward.
            var setting = new BusinessAssistantSetting
            {
                BusinessId = businessId,
                AssistantTypeId = assistant.Id,
                IsEnabled = existing?.IsEnabled ?? true,
                IsBrandingFooterEnabled = existing?.IsBrandingFooterEnabled ?? true,
                WorkingHoursStart = existing?.WorkingHoursStart,
                WorkingHoursEnd = existing?.WorkingHoursEnd,
                SendDayOfWeek = existing?.SendDayOfWeek,
                SendTimeLocal = existing?.SendTimeLocal,
                RecipientOverride = existing?.RecipientOverride,
                IsRecipientOwnerIncluded = existing?.IsRecipientOwnerIncluded ?? true,
                IncludedFiguresCsv = existing?.IncludedFiguresCsv
            };

            var isDailyBrief = assistant.Key == DigestAssistantKeys.DailyBrief;
            var isEventAlert = !assistant.IsCustomerFacing && assistant.Key == NotificationProducer.NewPaymentKey;

            if (assistant.IsCustomerFacing)
            {
                // Customer-facing assistant: working hours + branding footer.
                TimeOnly? start = null, end = null;
                if (!string.IsNullOrWhiteSpace(request.WorkingHoursStart) && !string.IsNullOrWhiteSpace(request.WorkingHoursEnd))
                {
                    if (!TimeOnly.TryParse(request.WorkingHoursStart, out var s) || !TimeOnly.TryParse(request.WorkingHoursEnd, out var e))
                        return Json(new { success = false, message = "Invalid working hours format." });
                    if (e <= s)
                        return Json(new { success = false, message = "End time must be after start time." });
                    start = s;
                    end = e;
                }
                setting.IsBrandingFooterEnabled = request.IsBrandingFooterEnabled;
                setting.WorkingHoursStart = start;
                setting.WorkingHoursEnd = end;
            }
            else
            {
                // Owner-facing assistants. Validate the recipient override (shared by all owner types).
                if (!string.IsNullOrWhiteSpace(request.RecipientOverride))
                {
                    var addresses = request.RecipientOverride
                        .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => x.Length > 0);
                    foreach (var addr in addresses)
                    {
                        try { _ = new System.Net.Mail.MailAddress(addr); }
                        catch { return Json(new { success = false, message = $"Invalid email address: {addr}" }); }
                    }
                }
                setting.RecipientOverride = string.IsNullOrWhiteSpace(request.RecipientOverride) ? null : request.RecipientOverride.Trim();
                setting.IsRecipientOwnerIncluded = request.IsRecipientOwnerIncluded;

                if (isEventAlert)
                {
                    // Event alert (e.g. New Payment): recipient only — no schedule, no figures.
                    // (setting.SendDayOfWeek / SendTimeLocal / IncludedFiguresCsv carried forward unchanged.)
                }
                else
                {
                    // Scheduled owner assistant: parse the send time (shared by weekly + daily).
                    TimeOnly? sendTime = null;
                    if (!string.IsNullOrWhiteSpace(request.SendTimeLocal))
                    {
                        if (!TimeOnly.TryParse(request.SendTimeLocal, out var t))
                            return Json(new { success = false, message = "Invalid send time format." });
                        sendTime = t;
                    }
                    setting.SendTimeLocal = sendTime;

                    if (isDailyBrief)
                    {
                        // Daily: no day-of-week (ignored); no figures.
                        setting.SendDayOfWeek = null;
                    }
                    else
                    {
                        // Weekly digest: day-of-week required + optional figures.
                        if (request.SendDayOfWeek is < 0 or > 6)
                            return Json(new { success = false, message = "Invalid send day." });
                        setting.SendDayOfWeek = request.SendDayOfWeek;
                        setting.IncludedFiguresCsv = string.IsNullOrWhiteSpace(request.IncludedFiguresCsv) ? null : request.IncludedFiguresCsv.Trim();
                    }
                }
            }

            await _settingRepository.UpsertAsync(setting);

            return Json(new { success = true, message = "Settings saved." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong. Please try again." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetAssistantLog(string assistantKey, int page = 1)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var assistant = await _dbContext.AssistantTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Key == assistantKey);
            if (assistant == null)
                return Json(new { success = false, message = "Assistant not found." });

            const int pageSize = 15;
            var rows = await _outboxRepository.GetByBusinessAndAssistantPagedAsync(businessId, assistant.Id, page, pageSize);

            var items = rows.Select(r => new
            {
                recipient = r.RecipientEmail,
                subject = r.Subject,
                status = StatusName(r.OutboxMessageStatusTypeId),
                scheduledForUtc = r.ScheduledForUtc,
                sentAtUtc = r.SentAtUtc,
                createdAtUtc = r.CreatedAtUtc
            });

            return Json(new { success = true, items, page });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load activity log." });
        }
    }

    private static string StatusName(int statusTypeId) => statusTypeId switch
    {
        OutboxMessageStatusTypes.Pending => "Pending",
        OutboxMessageStatusTypes.Sent => "Sent",
        OutboxMessageStatusTypes.Failed => "Failed",
        _ => "Pending"
    };
}
