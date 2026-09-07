using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Web.Models.Assistants;
using Portal.Web.Services;
using Portal.Web.Services.Notifications;

namespace Portal.Web.Controllers;

/// <summary>
/// SuperAdmin configuration for notification failure-alert recipients (stored in PlatformConfig).
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[Route("Admin/Notifications")]
public class AdminNotificationController : Controller
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IPlatformConfigService _platformConfigService;

    public AdminNotificationController(IPlatformConfigService platformConfigService)
    {
        _platformConfigService = platformConfigService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var raw = await _platformConfigService.GetValueAsync(NotificationAdminAlertService.RecipientsConfigKey);
            var model = new AdminNotificationRecipientsViewModel
            {
                Recipients = raw ?? string.Empty
            };
            return View(model);
        }
        catch (Exception ex)
        {
            return View(new AdminNotificationRecipientsViewModel());
        }
    }

    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSaveAdminRecipients([FromBody] SaveAdminRecipientsRequest request)
    {
        try
        {
            var raw = request.Recipients ?? string.Empty;

            var emails = raw
                .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => e.Length > 0)
                .ToList();

            var invalid = emails.Where(e => !EmailRegex.IsMatch(e)).ToList();
            if (invalid.Any())
            {
                return Json(new { success = false, message = $"Invalid email address(es): {string.Join(", ", invalid)}" });
            }

            // Normalise to a semicolon-delimited, de-duplicated list.
            var normalised = string.Join(";", emails.Distinct(StringComparer.OrdinalIgnoreCase));

            await _platformConfigService.SetValueAsync(NotificationAdminAlertService.RecipientsConfigKey, normalised);

            return Json(new { success = true, message = "Recipients saved." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong. Please try again." });
        }
    }
}
