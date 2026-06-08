using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Web.Services.Email;

namespace Portal.Web.Services.Billing;

/// <summary>
/// Defines the contract for sending invoice notification emails to business owners.
/// </summary>
public interface IInvoiceEmailService
{
    /// <summary>
    /// Sends an invoice notification email to the business owner.
    /// Called after the invoice creation transaction has committed.
    /// Failures are logged but do not throw.
    /// </summary>
    Task SendInvoiceNotificationAsync(int billingInvoiceId);
}

/// <summary>
/// Sends invoice notification emails after payment using EmailDepartmentEnum.Invoices.
/// Prevents duplicate sends using the IsEmailSent flag on the BillingInvoice.
/// Logs failures as warnings without rolling back invoice creation.
/// </summary>
public class InvoiceEmailService : IInvoiceEmailService
{
    private readonly PortalDbContext _portalDbContext;
    private readonly MembershipDbContext _membershipDbContext;
    private readonly IEmailSender _emailSender;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<InvoiceEmailService> _logger;

    public InvoiceEmailService(
        PortalDbContext portalDbContext,
        MembershipDbContext membershipDbContext,
        IEmailSender emailSender,
        IHttpContextAccessor httpContextAccessor,
        ILogger<InvoiceEmailService> logger)
    {
        _portalDbContext = portalDbContext;
        _membershipDbContext = membershipDbContext;
        _emailSender = emailSender;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendInvoiceNotificationAsync(int billingInvoiceId)
    {
        try
        {
            // Load the billing invoice
            var invoice = await _portalDbContext.BillingInvoices
                .FirstOrDefaultAsync(bi => bi.Id == billingInvoiceId);

            if (invoice == null)
            {
                _logger.LogWarning(
                    "InvoiceEmailService: BillingInvoice not found. BillingInvoiceId={BillingInvoiceId}",
                    billingInvoiceId);
                return;
            }

            // Check IsEmailSent flag — skip if already sent (prevents duplicates on webhook redelivery)
            if (invoice.IsEmailSent)
            {
                _logger.LogInformation(
                    "InvoiceEmailService: Email already sent for BillingInvoice. BillingInvoiceId={BillingInvoiceId}, InvoiceNumber={InvoiceNumber}",
                    billingInvoiceId, invoice.InvoiceNumber);
                return;
            }

            // Resolve business owner email from MembershipDbContext
            var ownerEmail = await _membershipDbContext.UserBusinesses
                .Include(ub => ub.User)
                .Where(ub => ub.BusinessId == invoice.BusinessId && ub.IsOwner && ub.IsActive)
                .Select(ub => ub.User.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(ownerEmail))
            {
                _logger.LogWarning(
                    "InvoiceEmailService: No email address found for business owner. BusinessId={BusinessId}, InvoiceNumber={InvoiceNumber}",
                    invoice.BusinessId, invoice.InvoiceNumber);
                return;
            }

            // Compose and send the email
            var subject = $"Your Invoice {invoice.InvoiceNumber} — Portal by 3 Inventors";
            var htmlBody = BuildInvoiceNotificationHtml(invoice, billingInvoiceId);

            await _emailSender.SendEmailAsync(ownerEmail, subject, htmlBody, EmailDepartmentEnum.Invoices);

            // Mark as sent and persist
            invoice.IsEmailSent = true;
            await _portalDbContext.SaveChangesAsync();

            _logger.LogInformation(
                "InvoiceEmailService: Invoice notification email sent. Recipient={Recipient}, InvoiceNumber={InvoiceNumber}",
                ownerEmail, invoice.InvoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "InvoiceEmailService: Failed to send invoice notification email. BillingInvoiceId={BillingInvoiceId}",
                billingInvoiceId);
            // Do not throw — email failure must not affect invoice creation
        }
    }

    private string BuildInvoiceNotificationHtml(
        Portal.Infrastructure.Entities.Billing.BillingInvoice invoice,
        int billingInvoiceId)
    {
        var baseUrl = GetBaseUrl();
        var downloadUrl = $"{baseUrl}/Account/Billing/DownloadInvoice/{billingInvoiceId}";
        var periodStart = invoice.PeriodStart.ToString("dd MMM yyyy");
        var periodEnd = invoice.PeriodEnd.ToString("dd MMM yyyy");
        var amount = $"€{invoice.AmountEur:N2}";

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" /></head>
<body style=""margin:0;padding:0;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;background-color:#F2F6FA;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#F2F6FA;"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:600px;width:100%;background-color:#FFFFFF;border-radius:16px;overflow:hidden;"">
                    <!-- Header -->
                    <tr>
                        <td style=""background-color:#F7FAFC;padding:32px 40px;text-align:center;border-bottom:1px solid #E2EBF3;"">
                            <img src=""https://www.3inventors.com/img/logo_blue_web_toolbar_oi.png"" alt=""3 Inventors"" width=""220"" style=""display:block;margin:0 auto;max-width:220px;height:auto;"" />
                        </td>
                    </tr>
                    <!-- Accent line -->
                    <tr><td style=""height:4px;background-color:#0D5EA6;""></td></tr>
                    <!-- Body -->
                    <tr>
                        <td style=""padding:40px;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr>
                                    <td style=""background-color:#EBF5FF;border-radius:20px;padding:6px 16px;"">
                                        <span style=""font-size:12px;font-weight:700;color:#0D5EA6;letter-spacing:0.06em;text-transform:uppercase;"">Subscription Invoice</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">Invoice {System.Net.WebUtility.HtmlEncode(invoice.InvoiceNumber ?? "")}</h1>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                A new subscription invoice has been generated for your account. Below are the details:
                            </p>
                            <!-- Invoice Details -->
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:28px 0;width:100%;"">
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#0D5EA6;letter-spacing:0.08em;text-transform:uppercase;"">Invoice Number</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{System.Net.WebUtility.HtmlEncode(invoice.InvoiceNumber ?? "N/A")}</div>
                                    </td>
                                </tr>
                                <tr><td style=""height:8px;""></td></tr>
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#0D5EA6;letter-spacing:0.08em;text-transform:uppercase;"">Billing Period</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{periodStart} — {periodEnd}</div>
                                    </td>
                                </tr>
                                <tr><td style=""height:8px;""></td></tr>
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#0D5EA6;letter-spacing:0.08em;text-transform:uppercase;"">Amount Charged</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{amount}</div>
                                    </td>
                                </tr>
                            </table>
                            <!-- CTA Button -->
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:28px auto 0 auto;"">
                                <tr>
                                    <td>
                                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-radius:12px;overflow:hidden;"">
                                            <tr>
                                                <td style=""background-color:#0D5EA6;padding:16px 40px;text-align:center;"">
                                                    <a href=""{downloadUrl}"" style=""color:#FFFFFF;font-size:16px;font-weight:700;text-decoration:none;display:block;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">Download Invoice PDF</a>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin:24px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;"">
                                You can also view your billing history and download invoices from your account settings.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color:#0B1B28;padding:28px 40px;text-align:center;"">
                            <p style=""margin:0 0 8px 0;font-size:14px;font-weight:700;color:#FFFFFF;"">3 Inventors Limited</p>
                            <p style=""margin:0 0 4px 0;font-size:12px;color:#8899A6;"">Nicosia, Cyprus</p>
                            <p style=""margin:0;font-size:11px;color:#5E6D7A;letter-spacing:0.08em;"">Knowledge &middot; Professionalism &middot; Innovation</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null) return "";
        return $"{request.Scheme}://{request.Host}";
    }
}
