using Microsoft.Extensions.Logging;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Stub implementation of IEmailService that logs invitation links instead of sending real emails.
/// Replace with a real implementation (e.g., SendGrid, SMTP) when ready.
/// </summary>
public class StubEmailService : IEmailService
{
    private readonly ILogger<StubEmailService> _logger;

    public StubEmailService(ILogger<StubEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendInvitationEmailAsync(string toEmail, string invitationLink, string businessName)
    {
        _logger.LogInformation(
            "Invitation email stub: To={ToEmail}, Business={BusinessName}, Link={InvitationLink}",
            toEmail, businessName, invitationLink);

        return Task.CompletedTask;
    }

    public Task SendProposalEmailAsync(string toEmail, string shareToken, string quotationReference, string businessName, DateTimeOffset expiresAtUtc)
    {
        _logger.LogInformation(
            "Proposal email stub: To={ToEmail}, Reference={Reference}, Business={BusinessName}, Token={Token}, Expires={Expires}",
            toEmail, quotationReference, businessName, shareToken, expiresAtUtc);

        return Task.CompletedTask;
    }

    public Task SendInvoiceEmailAsync(string toEmail, string shareToken, string invoiceNumber, string businessName, decimal totalAmount, DateOnly dueDate, DateTimeOffset expiresAtUtc)
    {
        _logger.LogInformation(
            "Invoice email stub: To={ToEmail}, Invoice={InvoiceNumber}, Business={BusinessName}, Token={Token}, Amount={Amount}, DueDate={DueDate}, Expires={Expires}",
            toEmail, invoiceNumber, businessName, shareToken, totalAmount, dueDate, expiresAtUtc);

        return Task.CompletedTask;
    }

    public Task SendStatementEmailAsync(string recipientEmail, string customerName, string businessName, byte[] pdfBytes, string filename)
    {
        _logger.LogInformation(
            "Statement email stub: To={RecipientEmail}, Customer={CustomerName}, Business={BusinessName}, Filename={Filename}, PdfSize={PdfSize} bytes",
            recipientEmail, customerName, businessName, filename, pdfBytes.Length);

        return Task.CompletedTask;
    }

    public Task SendDemoInvitationEmailAsync(string toEmail, string magicLink, string businessName, DateTime expiresAtUtc)
    {
        _logger.LogInformation(
            "Demo invitation email stub: To={ToEmail}, Business={BusinessName}, MagicLink={MagicLink}, ExpiresAtUtc={ExpiresAtUtc}",
            toEmail, businessName, magicLink, expiresAtUtc);

        return Task.CompletedTask;
    }

    public Task SendPaymentReminderEmailAsync(string toEmail, string customerName, string invoiceNumber, decimal outstandingAmount, DateOnly dueDate, string businessName, string escalationTier, string? invoiceShareToken, string baseUrl, string? trackingToken = null, bool isTestSend = false)
    {
        _logger.LogInformation(
            "Payment reminder email stub: To={ToEmail}, Customer={CustomerName}, Invoice={InvoiceNumber}, Amount={Amount}, DueDate={DueDate}, Business={BusinessName}, Tier={Tier}, IsTestSend={IsTestSend}",
            toEmail, customerName, invoiceNumber, outstandingAmount, dueDate, businessName, escalationTier, isTestSend);

        return Task.CompletedTask;
    }
}
