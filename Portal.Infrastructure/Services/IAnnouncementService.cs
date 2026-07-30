using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for feature announcements — visibility filtering, dismissals, and admin CRUD.
/// </summary>
public interface IAnnouncementService
{
    /// <summary>
    /// Returns all visible announcements for the user (filtered by active, published, not expired, plan tier).
    /// Each item includes an IsDismissed flag for the given user.
    /// </summary>
    Task<List<AnnouncementDto>> GetVisibleForUserAsync(string userId);

    /// <summary>
    /// Returns the unread count for the user (visible minus dismissed).
    /// </summary>
    Task<int> GetUnreadCountAsync(string userId);

    /// <summary>
    /// Returns the most recent visible undismissed announcement for the dashboard banner.
    /// Returns null if all announcements are dismissed.
    /// </summary>
    Task<AnnouncementDto?> GetBannerAnnouncementAsync(string userId);

    /// <summary>
    /// Dismisses a single announcement for the user (idempotent).
    /// Returns the updated unread count.
    /// </summary>
    Task<int> DismissAsync(string userId, int announcementId);

    /// <summary>
    /// Dismisses all visible undismissed announcements for the user.
    /// Returns the updated unread count (should be 0).
    /// </summary>
    Task<int> DismissAllAsync(string userId);

    /// <summary>
    /// Returns all announcements for admin management (includes inactive/expired).
    /// </summary>
    Task<List<AdminAnnouncementDto>> GetAllForAdminAsync();

    /// <summary>
    /// Returns a single announcement by Id for editing.
    /// </summary>
    Task<AdminAnnouncementDto?> GetByIdForAdminAsync(int id);

    /// <summary>
    /// Creates a new announcement. Returns the generated Id.
    /// </summary>
    Task<ServiceResult<int>> CreateAsync(CreateAnnouncementRequest request);

    /// <summary>
    /// Updates an existing announcement.
    /// </summary>
    Task<ServiceResult> UpdateAsync(UpdateAnnouncementRequest request);

    /// <summary>
    /// Toggles the IsActive flag for an announcement.
    /// </summary>
    Task<ServiceResult> ToggleActiveAsync(int id, bool isActive);
}
