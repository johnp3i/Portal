using Portal.Infrastructure.Models;

namespace Portal.Web.Models.ViewComponents;

/// <summary>
/// ViewModel for the WhatsNew ViewComponent — powers the topbar badge and slide-out panel.
/// </summary>
public class WhatsNewViewModel
{
    public List<AnnouncementDto> Announcements { get; set; } = new();
    public int UnreadCount { get; set; }
    public string BadgeText { get; set; } = string.Empty;
}
