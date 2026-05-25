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
}
