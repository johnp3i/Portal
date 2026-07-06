using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Constants;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.PaymentReminderManual)]
public class PaymentReminderController : Controller
{
    private readonly IPaymentReminderService _reminderService;
    private readonly IPaymentReminderScheduleService _scheduleService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IConfiguration _configuration;

    public PaymentReminderController(
        IPaymentReminderService reminderService,
        IPaymentReminderScheduleService scheduleService,
        ICurrentTenantService currentTenantService,
        IConfiguration configuration)
    {
        _reminderService = reminderService;
        _scheduleService = scheduleService;
        _currentTenantService = currentTenantService;
        _configuration = configuration;
    }

    // --- Page Actions ---

    /// <summary>
    /// Schedule configuration page — requires PaymentReminderAuto permission.
    /// </summary>
    [HttpGet]
    [ModuleAccess(PortalModules.PaymentReminderAuto)]
    public async Task<IActionResult> Settings()
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var schedule = await _scheduleService.GetScheduleAsync(businessId);
            ViewBag.ScheduledTimeUtc = _configuration.GetValue<string>("PaymentReminders:ScheduledTimeUtc", "06:00");
            return View(schedule);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Upcoming reminders preview page — requires PaymentReminderAuto permission.
    /// </summary>
    [HttpGet]
    [ModuleAccess(PortalModules.PaymentReminderAuto)]
    public IActionResult Upcoming()
    {
        ViewBag.ScheduledTimeUtc = _configuration.GetValue<string>("PaymentReminders:ScheduledTimeUtc", "06:00");
        return View();
    }

    /// <summary>
    /// Reminder History page — shows all reminders sent by the business.
    /// </summary>
    [HttpGet]
    public IActionResult History()
    {
        return View();
    }

    // --- Anonymous Endpoints ---

    /// <summary>
    /// Tracking pixel endpoint — anonymous, returns 1x1 transparent PNG.
    /// Records email open event for the given tracking token.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Duration = 0)]
    public async Task<IActionResult> Track(string token)
    {
        try
        {
            if (!string.IsNullOrEmpty(token))
            {
                await _reminderService.RecordOpenEventAsync(token);
            }
        }
        catch (Exception ex)
        {
            // Silently fail — never expose tracking errors to recipient
        }

        return File(TransparentPixel.Bytes, "image/png");
    }

    // --- AJAX Endpoints ---

    /// <summary>
    /// Saves the full reminder schedule for the current business.
    /// Requires PaymentReminderAuto permission (full access level).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.PaymentReminderAuto, AccessLevels.Full)]
    public async Task<IActionResult> AxPostSaveSchedule([FromBody] SaveScheduleViewModel model)
    {
        try
        {
            var validation = _scheduleService.ValidateSchedule(model.Tiers);
            if (!validation.Success)
                return Json(new { success = false, message = validation.Message });

            var businessId = _currentTenantService.CurrentBusinessId;
            await _scheduleService.SaveScheduleAsync(businessId, model.Tiers);
            return Json(new { success = true, message = "Reminder schedule saved successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Sends a manual reminder for a specific invoice at the given escalation tier.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSendManualReminder(int invoiceId, string tier)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var result = await _reminderService.SendManualReminderAsync(businessId, invoiceId, tier);

            if (result.CustomerOptedOut)
                return Json(new { success = true, customerOptedOut = true, message = result.ErrorMessage });

            return Json(new { success = result.Success, message = result.ErrorMessage ?? "Reminder sent successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Sends a test reminder to an alternate email address.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSendTestReminder(int invoiceId, string escalationTier, string testRecipientEmail)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var result = await _reminderService.SendTestReminderAsync(businessId, invoiceId, escalationTier, testRecipientEmail);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to send test reminder." });
        }
    }

    /// <summary>
    /// Returns the reminder history for a specific invoice.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AxGetReminderHistory(int invoiceId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var history = await _reminderService.GetHistoryByInvoiceAsync(businessId, invoiceId);
            return Json(new { success = true, data = history });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Returns dashboard widget data for the current week.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AxGetDashboardWidget()
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var data = await _reminderService.GetDashboardWidgetDataAsync(businessId);
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Returns projected upcoming reminders for the next N days.
    /// </summary>
    [HttpGet]
    [ModuleAccess(PortalModules.PaymentReminderAuto)]
    public async Task<IActionResult> AxGetUpcomingReminders(int daysAhead = 14, string? tier = null)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var data = await _reminderService.GetUpcomingRemindersAsync(businessId, daysAhead, tier);
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load upcoming reminders." });
        }
    }

    /// <summary>
    /// Returns paginated, filtered reminder history for the current business.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AxGetAllReminderHistory(
        string? tier = null,
        string? status = null,
        string? method = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? customer = null,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var result = await _reminderService.GetAllReminderHistoryAsync(
                businessId, tier, status, method, dateFrom, dateTo, customer, page, pageSize);

            return Json(new { success = true, data = result.Items, totalCount = result.TotalCount, page, pageSize });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load reminder history." });
        }
    }
}
