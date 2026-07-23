using Portal.Web.Services.Email;

namespace Portal.Web.Services;

/// <summary>
/// Sends identity-related emails (email confirmation, password reset) using the existing
/// email infrastructure. Exceptions are caught and logged — email failures are never
/// surfaced to the caller to avoid revealing delivery status to end users.
/// </summary>
public class IdentityEmailService : IIdentityEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<IdentityEmailService> _logger;

    public IdentityEmailService(IEmailSender emailSender, ILogger<IdentityEmailService> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task SendEmailConfirmationAsync(string email, string confirmationLink)
    {
        try
        {
            var subject = "Confirm your Portal account";
            var htmlBody = BuildConfirmationEmailHtml(confirmationLink);

            await _emailSender.SendEmailAsync(email, subject, htmlBody, EmailDepartmentEnum.ConfirmEmail);

            _logger.LogInformation("Email confirmation sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email confirmation to {Email}", email);
            // Do not rethrow — caller should not know about email failures
        }
    }

    public async Task SendPasswordResetAsync(string email, string resetLink)
    {
        try
        {
            var subject = "Reset your Portal password";
            var htmlBody = BuildPasswordResetEmailHtml(resetLink);

            await _emailSender.SendEmailAsync(email, subject, htmlBody, EmailDepartmentEnum.ForgotPassword);

            _logger.LogInformation("Password reset email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            // Do not rethrow — caller should not know about email failures
        }
    }

    private static string BuildConfirmationEmailHtml(string confirmationLink)
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
                                        <span style=""font-size:12px;font-weight:700;color:#0D5EA6;letter-spacing:0.06em;text-transform:uppercase;"">Account Verification</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">Confirm your email</h1>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                Thank you for registering on Portal. Please confirm your email address by clicking the button below.
                            </p>
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:28px 0 0 0;"">
                                <tr>
                                    <td align=""center"">
                                        <!--[if mso]>
                                        <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word""
                                            href=""{System.Net.WebUtility.HtmlEncode(confirmationLink)}""
                                            style=""height:48px;v-text-anchor:middle;width:220px;""
                                            arcsize=""14%"" strokecolor=""#0D5EA6"" fillcolor=""#0D5EA6"">
                                            <w:anchorlock/>
                                            <center style=""color:#ffffff;font-family:sans-serif;font-size:16px;font-weight:bold;"">
                                                Confirm Email
                                            </center>
                                        </v:roundrect>
                                        <![endif]-->
                                        <!--[if !mso]><!-->
                                        <a href=""{System.Net.WebUtility.HtmlEncode(confirmationLink)}"" target=""_blank""
                                           style=""display:inline-block;padding:14px 32px;background-color:#0D5EA6;color:#ffffff;font-size:16px;font-weight:700;font-family:'Inter',Arial,sans-serif;text-decoration:none;border-radius:8px;text-align:center;"">
                                            Confirm Email
                                        </a>
                                        <!--<![endif]-->
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin:24px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;"">
                                If you did not create an account on Portal, you can safely ignore this email.
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

    private static string BuildPasswordResetEmailHtml(string resetLink)
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
                                        <span style=""font-size:12px;font-weight:700;color:#0D5EA6;letter-spacing:0.06em;text-transform:uppercase;"">Password Reset</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">Reset your password</h1>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                We received a request to reset your Portal account password. Click the button below to set a new password.
                            </p>
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:28px 0 0 0;"">
                                <tr>
                                    <td align=""center"">
                                        <!--[if mso]>
                                        <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word""
                                            href=""{System.Net.WebUtility.HtmlEncode(resetLink)}""
                                            style=""height:48px;v-text-anchor:middle;width:220px;""
                                            arcsize=""14%"" strokecolor=""#0D5EA6"" fillcolor=""#0D5EA6"">
                                            <w:anchorlock/>
                                            <center style=""color:#ffffff;font-family:sans-serif;font-size:16px;font-weight:bold;"">
                                                Reset Password
                                            </center>
                                        </v:roundrect>
                                        <![endif]-->
                                        <!--[if !mso]><!-->
                                        <a href=""{System.Net.WebUtility.HtmlEncode(resetLink)}"" target=""_blank""
                                           style=""display:inline-block;padding:14px 32px;background-color:#0D5EA6;color:#ffffff;font-size:16px;font-weight:700;font-family:'Inter',Arial,sans-serif;text-decoration:none;border-radius:8px;text-align:center;"">
                                            Reset Password
                                        </a>
                                        <!--<![endif]-->
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin:24px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;"">
                                If you did not request a password reset, you can safely ignore this email. Your password will remain unchanged.
                            </p>
                            <p style=""margin:12px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;"">
                                This link will expire in 24 hours.
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
