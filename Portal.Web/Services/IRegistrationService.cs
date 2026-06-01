using Portal.Infrastructure.Entities.Identity;
using Portal.Web.Models;

namespace Portal.Web.Services;

/// <summary>
/// Handles public self-service registration: creating users, tracking pending registrations,
/// and managing the flow between registration and email confirmation.
/// </summary>
public interface IRegistrationService
{
    /// <summary>
    /// Creates a new user with EmailConfirmed = false, stores the selected plan in a
    /// PendingRegistration record, and sends a confirmation email.
    /// </summary>
    Task<RegistrationResult> RegisterAsync(RegisterViewModel model);

    /// <summary>
    /// Retrieves the pending registration record for the given user, or null if none exists.
    /// </summary>
    Task<PendingRegistration?> GetPendingRegistrationByUserIdAsync(string userId);

    /// <summary>
    /// Marks the pending registration as completed after email confirmation and Stripe checkout.
    /// </summary>
    Task MarkPendingRegistrationCompletedAsync(string userId);
}
