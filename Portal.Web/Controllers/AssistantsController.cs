using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Notification;
using Portal.Infrastructure.Repositories.Notification;
using Portal.Infrastructure.Services;
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
                    // A scheduled owner-facing digest is any non-customer-facing assistant.
                    var isDigest = !a.IsCustomerFacing;
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
                        IsScheduledDigest = isDigest,
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
                // Scheduled owner-facing digest: send day/time + recipient + figures.
                if (request.SendDayOfWeek is < 0 or > 6)
                    return Json(new { success = false, message = "Invalid send day." });

                TimeOnly? sendTime = null;
                if (!string.IsNullOrWhiteSpace(request.SendTimeLocal))
                {
                    if (!TimeOnly.TryParse(request.SendTimeLocal, out var t))
                        return Json(new { success = false, message = "Invalid send time format." });
                    sendTime = t;
                }

                // Validate any recipient override addresses (delimited list).
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

                setting.SendDayOfWeek = request.SendDayOfWeek;
                setting.SendTimeLocal = sendTime;
                setting.RecipientOverride = string.IsNullOrWhiteSpace(request.RecipientOverride) ? null : request.RecipientOverride.Trim();
                setting.IsRecipientOwnerIncluded = request.IsRecipientOwnerIncluded;
                setting.IncludedFiguresCsv = string.IsNullOrWhiteSpace(request.IncludedFiguresCsv) ? null : request.IncludedFiguresCsv.Trim();
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
