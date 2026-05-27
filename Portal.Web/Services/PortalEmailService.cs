using Microsoft.AspNetCore.Http;
using Portal.Infrastructure.Services;
using Portal.Web.Services.Email;

namespace Portal.Web.Services;

/// <summary>
/// Real implementation of IEmailService that delegates to the custom IEmailSender.
/// Sends branded invitation emails via SMTP using the InvitationRequest department.
/// </summary>
public class PortalEmailService : IEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<PortalEmailService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PortalEmailService(IEmailSender emailSender, ILogger<PortalEmailService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _emailSender = emailSender;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null) return "";
        return $"{request.Scheme}://{request.Host}";
    }

    public async Task SendInvitationEmailAsync(string toEmail, string invitationLink, string businessName)
    {
        try
        {
            var subject = $"You've been invited to join {businessName} on Portal";
            var htmlBody = BuildInvitationHtml(invitationLink, businessName);

            await _emailSender.SendEmailAsync(toEmail, subject, htmlBody, EmailDepartmentEnum.InvitationRequest);

            _logger.LogInformation("Invitation email sent to {Email} for business {Business}", toEmail, businessName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invitation email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendProposalEmailAsync(string toEmail, string shareToken, string quotationReference, string businessName, DateTimeOffset expiresAtUtc)
    {
        try
        {
            var subject = $"Proposal from {businessName} — {quotationReference}";
            var htmlBody = BuildProposalHtml(shareToken, quotationReference, businessName, expiresAtUtc, GetBaseUrl());

            await _emailSender.SendEmailAsync(toEmail, subject, htmlBody, EmailDepartmentEnum.Proposals);

            _logger.LogInformation("Proposal email sent to {Email} for reference {Reference}", toEmail, quotationReference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send proposal email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendInvoiceEmailAsync(string toEmail, string shareToken, string invoiceNumber, string businessName, decimal totalAmount, DateOnly dueDate, DateTimeOffset expiresAtUtc)
    {
        try
        {
            var subject = $"Invoice from {businessName} — {invoiceNumber}";
            var htmlBody = BuildInvoiceHtml(shareToken, invoiceNumber, businessName, totalAmount, dueDate, expiresAtUtc, GetBaseUrl());

            await _emailSender.SendEmailAsync(toEmail, subject, htmlBody, EmailDepartmentEnum.Invoices);

            _logger.LogInformation("Invoice email sent to {Email} for invoice {InvoiceNumber}", toEmail, invoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invoice email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendStatementEmailAsync(string recipientEmail, string customerName, string businessName, byte[] pdfBytes, string filename)
    {
        try
        {
            var subject = $"Statement of Account from {businessName}";
            var htmlBody = BuildStatementEmailHtml(customerName, businessName);

            await _emailSender.SendEmailWithAttachmentAsync(recipientEmail, subject, htmlBody, EmailDepartmentEnum.Invoices, pdfBytes, filename, "application/pdf");

            _logger.LogInformation("Statement email sent to {Email} for customer {CustomerName}", recipientEmail, customerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send statement email to {Email}", recipientEmail);
            throw;
        }
    }

    private static string BuildInvitationHtml(string invitationLink, string businessName)
    {
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
                                        <span style=""font-size:12px;font-weight:700;color:#0D5EA6;letter-spacing:0.06em;text-transform:uppercase;"">Invitation</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">You're invited!</h1>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                You have been invited to join <strong>{System.Net.WebUtility.HtmlEncode(businessName)}</strong> on the Portal platform.
                            </p>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                Click the button below to create your account and get started.
                            </p>
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:28px 0 0 0;"">
                                <tr>
                                    <td align=""center"" bgcolor=""#0D5EA6"" style=""background-color:#0D5EA6;border-radius:8px;"">
                                        <a href=""{System.Net.WebUtility.HtmlEncode(invitationLink)}"" target=""_blank"" style=""display:inline-block;padding:14px 32px;font-size:16px;font-weight:700;font-family:'Inter',Arial,sans-serif;color:#FFFFFF;text-decoration:none;border-radius:8px;mso-padding-alt:0;text-align:center;"">
                                            <!--[if mso]><i style=""letter-spacing:32px;mso-font-width:-100%;mso-text-raise:21pt"">&nbsp;</i><![endif]-->
                                            <span style=""mso-text-raise:10pt;"">Create Account</span>
                                            <!--[if mso]><i style=""letter-spacing:32px;mso-font-width:-100%"">&nbsp;</i><![endif]-->
                                        </a>
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin:24px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;"">
                                This invitation expires in 72 hours. If you didn't expect this email, you can safely ignore it.
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

    private static string BuildProposalHtml(string shareToken, string quotationReference, string businessName, DateTimeOffset expiresAtUtc, string baseUrl)
    {
        var proposalUrl = $"{baseUrl}/proposal/{System.Net.WebUtility.UrlEncode(shareToken)}";
        var expirationFormatted = expiresAtUtc.ToString("dd MMMM yyyy");

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" /></head>
<body style=""margin:0;padding:0;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;background-color:#F2F6FA;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#F2F6FA;"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:600px;width:100%;background-color:#FFFFFF;border-radius:16px;overflow:hidden;"">
                    <tr>
                        <td style=""background-color:#F7FAFC;padding:32px 40px;text-align:center;border-bottom:1px solid #E2EBF3;"">
                            <img src=""https://www.3inventors.com/img/logo_blue_web_toolbar_oi.png"" alt=""3 Inventors"" width=""220"" style=""display:block;margin:0 auto;max-width:220px;height:auto;"" />
                        </td>
                    </tr>
                    <tr><td style=""height:4px;background-color:#0D5EA6;""></td></tr>
                    <tr>
                        <td style=""padding:40px;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr>
                                    <td style=""background-color:#EBF5FF;border-radius:20px;padding:6px 16px;"">
                                        <span style=""font-size:12px;font-weight:700;color:#0D5EA6;letter-spacing:0.06em;text-transform:uppercase;"">Proposal</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">You have a new proposal</h1>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                <strong>{System.Net.WebUtility.HtmlEncode(businessName)}</strong> has shared a proposal with you.
                            </p>
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;width:100%;"">
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#0D5EA6;letter-spacing:0.08em;text-transform:uppercase;"">Reference</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{System.Net.WebUtility.HtmlEncode(quotationReference)}</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;margin-top:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#0D5EA6;letter-spacing:0.08em;text-transform:uppercase;"">Expires</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{expirationFormatted}</div>
                                    </td>
                                </tr>
                            </table>
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:28px auto 0 auto;"">
                                <tr>
                                    <td>
                                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-radius:12px;overflow:hidden;"">
                                            <tr>
                                                <td style=""background-color:#0D5EA6;padding:16px 40px;text-align:center;"">
                                                    <a href=""{proposalUrl}"" style=""color:#FFFFFF;font-size:16px;font-weight:700;text-decoration:none;display:block;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">View Proposal</a>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin:24px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;"">
                                This link will expire on {expirationFormatted}. After that date, you'll need to request a new link from {System.Net.WebUtility.HtmlEncode(businessName)}.
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""background-color:#0B1B28;padding:28px 40px;text-align:center;"">
                            <p style=""margin:0 0 8px 0;font-size:14px;font-weight:700;color:#FFFFFF;"">{System.Net.WebUtility.HtmlEncode(businessName)}</p>
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

    private static string BuildInvoiceHtml(string shareToken, string invoiceNumber, string businessName, decimal totalAmount, DateOnly dueDate, DateTimeOffset expiresAtUtc, string baseUrl)
    {
        var invoiceUrl = $"{baseUrl}/invoice-view/{System.Net.WebUtility.UrlEncode(shareToken)}";
        var dueDateFormatted = dueDate.ToString("dd MMMM yyyy");
        var expirationFormatted = expiresAtUtc.ToString("dd MMMM yyyy");
        var amountFormatted = totalAmount.ToString("N2");

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" /></head>
<body style=""margin:0;padding:0;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;background-color:#F2F6FA;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#F2F6FA;"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:600px;width:100%;background-color:#FFFFFF;border-radius:16px;overflow:hidden;"">
                    <tr>
                        <td style=""background-color:#F7FAFC;padding:32px 40px;text-align:center;border-bottom:1px solid #E2EBF3;"">
                            <img src=""https://www.3inventors.com/img/logo_blue_web_toolbar_oi.png"" alt=""3 Inventors"" width=""220"" style=""display:block;margin:0 auto;max-width:220px;height:auto;"" />
                        </td>
                    </tr>
                    <tr><td style=""height:4px;background-color:#129867;""></td></tr>
                    <tr>
                        <td style=""padding:40px;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr>
                                    <td style=""background-color:#E6F7F1;border-radius:20px;padding:6px 16px;"">
                                        <span style=""font-size:12px;font-weight:700;color:#129867;letter-spacing:0.06em;text-transform:uppercase;"">Invoice</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">You have a new invoice</h1>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                <strong>{System.Net.WebUtility.HtmlEncode(businessName)}</strong> has shared an invoice with you.
                            </p>
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;width:100%;"">
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#129867;letter-spacing:0.08em;text-transform:uppercase;"">Invoice Number</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{System.Net.WebUtility.HtmlEncode(invoiceNumber)}</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;margin-top:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#129867;letter-spacing:0.08em;text-transform:uppercase;"">Total Amount</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{amountFormatted}</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;margin-top:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#129867;letter-spacing:0.08em;text-transform:uppercase;"">Due Date</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{dueDateFormatted}</div>
                                    </td>
                                </tr>
                            </table>
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:28px auto 0 auto;"">
                                <tr>
                                    <td>
                                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-radius:12px;overflow:hidden;"">
                                            <tr>
                                                <td style=""background-color:#129867;padding:16px 40px;text-align:center;"">
                                                    <a href=""{invoiceUrl}"" style=""color:#FFFFFF;font-size:16px;font-weight:700;text-decoration:none;display:block;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">View Invoice</a>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin:24px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;"">
                                This link will expire on {expirationFormatted}. After that date, you'll need to request a new link from {System.Net.WebUtility.HtmlEncode(businessName)}.
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""background-color:#0B1B28;padding:28px 40px;text-align:center;"">
                            <p style=""margin:0 0 8px 0;font-size:14px;font-weight:700;color:#FFFFFF;"">{System.Net.WebUtility.HtmlEncode(businessName)}</p>
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

    private static string BuildStatementEmailHtml(string customerName, string businessName)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" /></head>
<body style=""margin:0;padding:0;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;background-color:#F2F6FA;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#F2F6FA;"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:600px;width:100%;background-color:#FFFFFF;border-radius:16px;overflow:hidden;"">
                    <tr>
                        <td style=""background-color:#F7FAFC;padding:32px 40px;text-align:center;border-bottom:1px solid #E2EBF3;"">
                            <img src=""https://www.3inventors.com/img/logo_blue_web_toolbar_oi.png"" alt=""3 Inventors"" width=""220"" style=""display:block;margin:0 auto;max-width:220px;height:auto;"" />
                        </td>
                    </tr>
                    <tr><td style=""height:4px;background-color:#0D5EA6;""></td></tr>
                    <tr>
                        <td style=""padding:40px;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr>
                                    <td style=""background-color:#EBF5FF;border-radius:20px;padding:6px 16px;"">
                                        <span style=""font-size:12px;font-weight:700;color:#0D5EA6;letter-spacing:0.06em;text-transform:uppercase;"">Statement</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">Statement of Account</h1>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                Dear {System.Net.WebUtility.HtmlEncode(customerName)},
                            </p>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                Please find attached your statement of account from <strong>{System.Net.WebUtility.HtmlEncode(businessName)}</strong>.
                            </p>
                            <p style=""margin:16px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;"">
                                If you have any questions regarding this statement, please do not hesitate to contact us.
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""background-color:#0B1B28;padding:28px 40px;text-align:center;"">
                            <p style=""margin:0 0 8px 0;font-size:14px;font-weight:700;color:#FFFFFF;"">{System.Net.WebUtility.HtmlEncode(businessName)}</p>
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
}
