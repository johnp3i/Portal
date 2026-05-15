namespace Portal.Infrastructure.Services;

/// <summary>
/// Interface for sending emails (invitation emails, notifications, etc.).
/// </summary>
public interface IEmailService
{
    Task SendInvitationEmailAsync(string toEmail, string invitationLink, string businessName);
    Task SendProposalEmailAsync(string toEmail, string shareToken, string quotationReference, string businessName, DateTimeOffset expiresAtUtc);
}
