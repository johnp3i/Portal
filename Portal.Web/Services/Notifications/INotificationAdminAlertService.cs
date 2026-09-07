namespace Portal.Web.Services.Notifications;

/// <summary>
/// Detects persistent notification-delivery failures and alerts administrators
/// (email + surfaced in the system log), throttled to at most once per window.
/// </summary>
public interface INotificationAdminAlertService
{
    Task CheckAndAlertAsync();
}
