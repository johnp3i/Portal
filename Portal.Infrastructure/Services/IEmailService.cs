namespace Portal.Infrastructure.Services;

/// <summary>
/// Interface for sending emails (invitation emails, notifications, etc.).
/// </summary>
public interface IEmailService
{
    Task SendInvitationEmailAsync(string toEmail, string invitationLink, string businessName);
    Task SendProposalEmailAsync(string toEmail, string shareToken, string quotationReference, string businessName, DateTimeOffset expiresAtUtc);
    Task SendInvoiceEmailAsync(string toEmail, string shareToken, string invoiceNumber, string businessName, decimal totalAmount, DateOnly dueDate, DateTimeOffset expiresAtUtc);

    /// <summary>
    /// Sends a statement of account email with the PDF attached.
    /// </summary>
    Task SendStatementEmailAsync(string recipientEmail, string customerName, string businessName, byte[] pdfBytes, string filename);
}
