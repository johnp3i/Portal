using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;

namespace Portal.Web.ViewComponents;

/// <summary>
/// Renders a dismissible banner card on the Dashboard highlighting the most recent unread announcement.
/// Returns empty content if no undismissed announcements exist or user is not authenticated.
/// </summary>
public class WhatsNewBannerViewComponent : ViewComponent
{
    private readonly IAnnouncementService _announcementService;
    private readonly ILogger<WhatsNewBannerViewComponent> _logger;

    public WhatsNewBannerViewComponent(IAnnouncementService announcementService, ILogger<WhatsNewBannerViewComponent> logger)
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

            var banner = await _announcementService.GetBannerAnnouncementAsync(userId);
            if (banner == null)
                return Content(string.Empty);

            return View(banner);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsNewBannerViewComponent failed — returning empty content.");
            return Content(string.Empty);
        }
    }
}
