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

    public async Task SendDemoInvitationEmailAsync(string toEmail, string magicLink, string businessName, DateTime expiresAtUtc)
    {
        try
        {
            var subject = $"You're invited to explore {businessName} on Portal";
            var htmlBody = BuildDemoInvitationHtml(magicLink, businessName, expiresAtUtc);

            await _emailSender.SendEmailAsync(toEmail, subject, htmlBody, EmailDepartmentEnum.Demo);

            _logger.LogInformation("Demo invitation email sent to {Email} for business {Business}", toEmail, businessName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send demo invitation email to {Email}", toEmail);
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
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:28px 0 0 0;"">
                                <tr>
                                    <td align=""center"">
                                        <!--[if mso]><v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word"" href=""{System.Net.WebUtility.HtmlEncode(invitationLink)}"" style=""height:48px;v-text-anchor:middle;width:240px;"" arcsize=""10%"" strokecolor=""#0D5EA6"" fillcolor=""#0D5EA6""><w:anchorlock/><center style=""color:#ffffff;font-family:sans-serif;font-size:15px;font-weight:bold;"">Create Account</center></v:roundrect><![endif]-->
                                        <!--[if !mso]><!--><a href=""{System.Net.WebUtility.HtmlEncode(invitationLink)}"" target=""_blank"" style=""display:inline-block;padding:14px 48px;background-color:#0D5EA6;color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;border-radius:6px;letter-spacing:0.3px;"">Create Account</a><!--<![endif]-->
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
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:28px 0 0 0;"">
                                <tr>
                                    <td align=""center"">
                                        <!--[if mso]><v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word"" href=""{proposalUrl}"" style=""height:48px;v-text-anchor:middle;width:240px;"" arcsize=""10%"" strokecolor=""#0D5EA6"" fillcolor=""#0D5EA6""><w:anchorlock/><center style=""color:#ffffff;font-family:sans-serif;font-size:15px;font-weight:bold;"">View Proposal</center></v:roundrect><![endif]-->
                                        <!--[if !mso]><!--><a href=""{proposalUrl}"" target=""_blank"" style=""display:inline-block;padding:14px 48px;background-color:#0D5EA6;color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;border-radius:6px;letter-spacing:0.3px;"">View Proposal</a><!--<![endif]-->
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
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:28px 0 0 0;"">
                                <tr>
                                    <td align=""center"">
                                        <!--[if mso]><v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word"" href=""{invoiceUrl}"" style=""height:48px;v-text-anchor:middle;width:240px;"" arcsize=""10%"" strokecolor=""#129867"" fillcolor=""#129867""><w:anchorlock/><center style=""color:#ffffff;font-family:sans-serif;font-size:15px;font-weight:bold;"">View Invoice</center></v:roundrect><![endif]-->
                                        <!--[if !mso]><!--><a href=""{invoiceUrl}"" target=""_blank"" style=""display:inline-block;padding:14px 48px;background-color:#129867;color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;border-radius:6px;letter-spacing:0.3px;"">View Invoice</a><!--<![endif]-->
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

    public async Task SendPaymentReminderEmailAsync(
        string toEmail, string customerName, string invoiceNumber,
        decimal outstandingAmount, DateOnly dueDate, string businessName,
        string escalationTier, string? invoiceShareToken, string baseUrl,
        string? trackingToken = null, bool isTestSend = false)
    {
        try
        {
            var subject = escalationTier switch
            {
                "Friendly" => $"Invoice approaching due date — {invoiceNumber}",
                "Firm" => $"Invoice overdue — action required — {invoiceNumber}",
                "Formal" => $"Final payment notice — {invoiceNumber}",
                _ => $"Payment reminder — {invoiceNumber}"
            };

            // Prefix subject with [TEST] for test sends
            if (isTestSend)
            {
                subject = "[TEST] " + subject;
            }

            var htmlBody = BuildPaymentReminderHtml(
                customerName, invoiceNumber, outstandingAmount, dueDate,
                businessName, escalationTier, invoiceShareToken, baseUrl);

            // Inject tracking pixel before closing </body> tag
            if (!string.IsNullOrEmpty(trackingToken))
            {
                var pixelHtml = $"<img src=\"{baseUrl}/PaymentReminder/Track/{trackingToken}\" width=\"1\" height=\"1\" style=\"display:block\" alt=\"\" />";
                htmlBody = htmlBody.Replace("</body>", pixelHtml + "</body>");
            }

            await _emailSender.SendEmailAsync(toEmail, subject, htmlBody, EmailDepartmentEnum.PaymentReminder);

            _logger.LogInformation(
                "Payment reminder ({Tier}) sent to {Email} for invoice {InvoiceNumber}",
                escalationTier, toEmail, invoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment reminder to {Email} for invoice {InvoiceNumber}", toEmail, invoiceNumber);
            throw;
        }
    }

    private static string BuildPaymentReminderHtml(
        string customerName, string invoiceNumber, decimal outstandingAmount,
        DateOnly dueDate, string businessName, string escalationTier,
        string? invoiceShareToken, string baseUrl)
    {
        var encodedCustomerName = System.Net.WebUtility.HtmlEncode(customerName);
        var encodedInvoiceNumber = System.Net.WebUtility.HtmlEncode(invoiceNumber);
        var encodedBusinessName = System.Net.WebUtility.HtmlEncode(businessName);
        var amountFormatted = $"\u20ac{outstandingAmount:N2}";
        var dueDateFormatted = dueDate.ToString("dd MMMM yyyy");

        // Tier-specific values
        string accentColor, badgeBg, badgeText, badgeLabel, heading, ctaLabel;
        string bodyContent, footerNote;

        switch (escalationTier)
        {
            case "Firm":
                accentColor = "#C8912E";
                badgeBg = "#FFF6E8";
                badgeText = "#C8912E";
                badgeLabel = "PAYMENT OVERDUE";
                heading = "Invoice overdue — action required";
                ctaLabel = "Pay Now";
                bodyContent = $@"
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                Dear {encodedCustomerName},
                            </p>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                We would like to draw your attention to invoice <strong>{encodedInvoiceNumber}</strong> for <strong>{amountFormatted}</strong> which was due on <strong>{dueDateFormatted}</strong> and remains unpaid. Please arrange payment at your earliest convenience.
                            </p>";
                footerNote = "If you have already made this payment, please allow 2-3 business days for processing.";
                break;

            case "Formal":
                accentColor = "#C24A4A";
                badgeBg = "#FDEAEA";
                badgeText = "#C24A4A";
                badgeLabel = "FINAL NOTICE";
                heading = "Final payment notice";
                ctaLabel = "Settle Invoice";
                bodyContent = $@"
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                Dear {encodedCustomerName},
                            </p>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                Despite previous correspondence, invoice <strong>{encodedInvoiceNumber}</strong> for <strong>{amountFormatted}</strong> (due <strong>{dueDateFormatted}</strong>) remains outstanding. This is a formal notice that we require immediate payment.
                            </p>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                If payment is not received within 7 days, we may need to consider further action. Please contact us if you wish to discuss payment arrangements.
                            </p>";
                footerNote = "If you believe this notice was sent in error, please contact us immediately.";
                break;

            default: // Friendly
                accentColor = "#0D5EA6";
                badgeBg = "#EBF5FF";
                badgeText = "#0D5EA6";
                badgeLabel = "PAYMENT REMINDER";
                heading = "Invoice approaching due date";
                ctaLabel = "View Invoice";
                bodyContent = $@"
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                Dear {encodedCustomerName},
                            </p>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                This is a friendly reminder that invoice <strong>{encodedInvoiceNumber}</strong> for <strong>{amountFormatted}</strong> is due on <strong>{dueDateFormatted}</strong>. We would appreciate your prompt attention to this matter.
                            </p>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                If payment has already been made, please disregard this message.
                            </p>";
                footerNote = $"This is an automated reminder from {encodedBusinessName}.";
                break;
        }

        // Build CTA button — only include href if share token is provided
        string ctaButton;
        if (!string.IsNullOrEmpty(invoiceShareToken))
        {
            var invoiceUrl = $"{baseUrl}/invoice/{System.Net.WebUtility.UrlEncode(invoiceShareToken)}";
            ctaButton = $@"
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:28px 0 0 0;"">
                                <tr>
                                    <td align=""center"">
                                        <!--[if mso]><v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word"" href=""{System.Net.WebUtility.HtmlEncode(invoiceUrl)}"" style=""height:48px;v-text-anchor:middle;width:240px;"" arcsize=""10%"" strokecolor=""{accentColor}"" fillcolor=""{accentColor}""><w:anchorlock/><center style=""color:#ffffff;font-family:sans-serif;font-size:15px;font-weight:bold;"">{ctaLabel}</center></v:roundrect><![endif]-->
                                        <!--[if !mso]><!--><a href=""{System.Net.WebUtility.HtmlEncode(invoiceUrl)}"" target=""_blank"" style=""display:inline-block;padding:14px 48px;background-color:{accentColor};color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;border-radius:6px;letter-spacing:0.3px;font-family:'Segoe UI',sans-serif;"">{ctaLabel}</a><!--<![endif]-->
                                    </td>
                                </tr>
                            </table>";
        }
        else
        {
            ctaButton = $@"
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:28px 0 0 0;"">
                                <tr>
                                    <td align=""center"">
                                        <span style=""display:inline-block;padding:14px 48px;background-color:{accentColor};color:#ffffff;font-size:15px;font-weight:700;border-radius:6px;letter-spacing:0.3px;font-family:'Segoe UI',sans-serif;"">{ctaLabel}</span>
                                    </td>
                                </tr>
                            </table>";
        }

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
                    <tr><td style=""height:4px;background-color:{accentColor};""></td></tr>
                    <!-- Body -->
                    <tr>
                        <td style=""padding:40px;"">
                            <!-- Badge -->
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr>
                                    <td style=""background-color:{badgeBg};border-radius:20px;padding:6px 16px;"">
                                        <span style=""font-size:12px;font-weight:700;color:{badgeText};letter-spacing:0.06em;text-transform:uppercase;"">{badgeLabel}</span>
                                    </td>
                                </tr>
                            </table>
                            <!-- Heading -->
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">{heading}</h1>
                            <!-- Body text -->{bodyContent}
                            <!-- CTA Button -->{ctaButton}
                            <p style=""margin:24px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                {footerNote}
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color:#0B1B28;padding:28px 40px;text-align:center;"">
                            <p style=""margin:0 0 8px 0;font-size:14px;font-weight:700;color:#FFFFFF;font-family:'Segoe UI',sans-serif;"">{encodedBusinessName}</p>
                            <p style=""margin:0;font-size:11px;color:#5E6D7A;letter-spacing:0.08em;font-family:'Segoe UI',sans-serif;"">Powered by 3 Inventors</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private static string BuildDemoInvitationHtml(string magicLink, string businessName, DateTime expiresAtUtc)
    {
        var expiryFormatted = expiresAtUtc.ToString("dd MMMM yyyy 'at' HH:mm 'UTC'");

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
                                        <span style=""font-size:12px;font-weight:700;color:#0D5EA6;letter-spacing:0.06em;text-transform:uppercase;"">Demo Invitation</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">You're invited to explore {System.Net.WebUtility.HtmlEncode(businessName)}</h1>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                You've been invited to explore <strong>{System.Net.WebUtility.HtmlEncode(businessName)}</strong> on the Portal platform. Click the button below to access the demo — no account creation needed.
                            </p>
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;width:100%;"">
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#0D5EA6;letter-spacing:0.08em;text-transform:uppercase;"">Access Expires</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{expiryFormatted}</div>
                                    </td>
                                </tr>
                            </table>
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:28px 0 0 0;"">
                                <tr>
                                    <td align=""center"">
                                        <!--[if mso]><v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word"" href=""{System.Net.WebUtility.HtmlEncode(magicLink)}"" style=""height:48px;v-text-anchor:middle;width:240px;"" arcsize=""10%"" strokecolor=""#0D5EA6"" fillcolor=""#0D5EA6""><w:anchorlock/><center style=""color:#ffffff;font-family:sans-serif;font-size:15px;font-weight:bold;"">Explore Demo</center></v:roundrect><![endif]-->
                                        <!--[if !mso]><!--><a href=""{System.Net.WebUtility.HtmlEncode(magicLink)}"" target=""_blank"" style=""display:inline-block;padding:14px 48px;background-color:#0D5EA6;color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;border-radius:6px;letter-spacing:0.3px;"">Explore Demo</a><!--<![endif]-->
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin:24px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;"">
                                This demo link expires on {expiryFormatted}. If you didn't expect this email, you can safely ignore it.
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
}
