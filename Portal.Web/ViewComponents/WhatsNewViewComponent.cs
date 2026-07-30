using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;
using Portal.Web.Models.ViewComponents;

namespace Portal.Web.ViewComponents;

/// <summary>
/// Renders the What's New sparkle icon with unread badge and slide-out panel in the topbar.
/// Returns empty content if user is not authenticated or on error (never breaks page layout).
/// </summary>
public class WhatsNewViewComponent : ViewComponent
{
    private readonly IAnnouncementService _announcementService;
    private readonly ILogger<WhatsNewViewComponent> _logger;

    public WhatsNewViewComponent(IAnnouncementService announcementService, ILogger<WhatsNewViewComponent> logger)
    {
        _announcementService = announcementService;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            if (UserClaimsPrincipal?.Identity?.IsAuthenticated != true)
                return Content(string.Empty);

            var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Content(string.Empty);

            var announcements = await _announcementService.GetVisibleForUserAsync(userId);
            var unreadCount = announcements.Count(a => !a.IsDismissed);

            var model = new WhatsNewViewModel
            {
                Announcements = announcements,
                UnreadCount = unreadCount,
                BadgeText = unreadCount > 9 ? "9+" : unreadCount.ToString()
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsNewViewComponent failed — returning empty content.");
            return Content(string.Empty);
        }
    }
}
