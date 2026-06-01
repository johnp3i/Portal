namespace Portal.Web.Services;

/// <summary>
/// Sends identity-related emails for the public registration and password reset flows.
/// </summary>
public interface IIdentityEmailService
{
    /// <summary>
    /// Sends an email verification link to the newly registered user.
    /// </summary>
    Task SendEmailConfirmationAsync(string email, string confirmationLink);

    /// <summary>
    /// Sends a password reset link to the user.
    /// </summary>
    Task SendPasswordResetAsync(string email, string resetLink);
}
