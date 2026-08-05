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

    /// <summary>
    /// Sends a demo invitation email with a magic link for auto-authentication into a demo business.
    /// </summary>
    Task SendDemoInvitationEmailAsync(string toEmail, string magicLink, string businessName, DateTime expiresAtUtc);

    /// <summary>
    /// Sends a payment reminder email with tier-specific subject line and template.
    /// </summary>
    /// <param name="trackingToken">Optional tracking token for open tracking pixel injection.</param>
    /// <param name="isTestSend">When true, prefixes the email subject with [TEST].</param>
    Task SendPaymentReminderEmailAsync(
        string toEmail, string customerName, string invoiceNumber,
        decimal outstandingAmount, DateOnly dueDate, string businessName,
        string escalationTier, string? invoiceShareToken, string baseUrl,
        string? trackingToken = null, bool isTestSend = false);

    /// <summary>
    /// Sends a payslip email with the PDF attached.
    /// </summary>
    Task SendPayslipEmailAsync(string toEmail, string employeeName, string businessName, string monthName, int year, byte[] pdfBytes, string filename);
}
