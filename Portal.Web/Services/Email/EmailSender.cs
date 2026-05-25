using MailKit.Security;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using MailKit.Net.Smtp;
using Portal.Web.Models;

namespace Portal.Web.Services.Email
{
    public class EmailSender : IEmailSender
    {
        private readonly List<EmailAccount> _emailAccounts;
        private readonly WebsiteSettings _websiteSettings;
        private readonly ILogger<EmailSender> _logger;
        public EmailSender(List<EmailAccount> emailAccounts, WebsiteSettings websiteSettings, ILogger<EmailSender> logger)
        {
            _emailAccounts = emailAccounts;
            _websiteSettings = websiteSettings;
            _logger = logger;
        }
        public async Task SendEmailAsync(string email, string subject, string message, EmailDepartmentEnum department)
        {
            try
            {
                _logger.LogInformation("Attempting to send email to: {Email} for department: {Department}", email, department);

                var account = _emailAccounts.FirstOrDefault(x => x.Department == department);
                if (account == null)
                {
                    _logger.LogError("No email account configured for department: {Department}", department);
                    throw new InvalidOperationException($"No email account configured for department: {department}");
                }

                var emailMessage = new MimeMessage();
                emailMessage.From.Add(new MailboxAddress(account.SenderEmail ?? account.Username, account.SenderEmail ?? account.Username));
                emailMessage.To.Add(new MailboxAddress(email, email));
                emailMessage.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = message
                };
                emailMessage.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    // Disable certificate validation for development/testing
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    // Set a longer timeout
                    client.Timeout = 60000; // 60 seconds

                    bool connected = false;
                    Exception? lastException = null;

                    // Try different connection modes in order of preference
                    // Note: mail.3inventors.com:587 uses SSL/TLS (implicit SSL), not STARTTLS
                    var connectionModes = new[]
                    {
                        (SecureSocketOptions.SslOnConnect, "SSL/TLS"),
                        (SecureSocketOptions.StartTls, "STARTTLS"),
                        (SecureSocketOptions.Auto, "Auto"),
                        (SecureSocketOptions.None, "Plain (no encryption)")
                    };

                    foreach (var (mode, modeName) in connectionModes)
                    {
                        try
                        {
                            _logger.LogInformation("Attempting {Mode} connection to {Server}:{Port}",
                                modeName, _websiteSettings.EmailServer.SmtpAddress, _websiteSettings.EmailServer.SmtpPort);

                            await client.ConnectAsync(
                                _websiteSettings.EmailServer.SmtpAddress,
                                _websiteSettings.EmailServer.SmtpPort,
                                mode);

                            connected = true;
                            _logger.LogInformation("Successfully connected using {Mode}", modeName);
                            break;
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            _logger.LogWarning(ex, "{Mode} connection failed: {Message}", modeName, ex.Message);
                        }
                    }

                    if (!connected)
                    {
                        throw new InvalidOperationException(
                            $"Failed to connect to SMTP server after trying all connection modes. Last error: {lastException?.Message}",
                            lastException);
                    }

                    // Authenticate
                    _logger.LogInformation("Authenticating as {Username}", account.Username);
                    await client.AuthenticateAsync(account.Username, account.Password);
                    _logger.LogInformation("Authentication successful");

                    // Send email
                    _logger.LogInformation("Sending email...");
                    await client.SendAsync(emailMessage);
                    _logger.LogInformation("Email sent successfully");

                    // Disconnect
                    await client.DisconnectAsync(true);

                    _logger.LogInformation("Successfully sent email to: {Email}", email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {Email}. Error type: {ErrorType}, Message: {Message}",
                    email, ex.GetType().Name, ex.Message);
                throw; // Re-throw to allow caller to handle
            }
        }

        public async Task SendEmailWithAttachmentAsync(string email, string subject, string message, EmailDepartmentEnum department, byte[] attachmentBytes, string attachmentFilename, string attachmentContentType)
        {
            try
            {
                _logger.LogInformation("Attempting to send email with attachment to: {Email} for department: {Department}", email, department);

                var account = _emailAccounts.FirstOrDefault(x => x.Department == department);
                if (account == null)
                {
                    _logger.LogError("No email account configured for department: {Department}", department);
                    throw new InvalidOperationException($"No email account configured for department: {department}");
                }

                var emailMessage = new MimeMessage();
                emailMessage.From.Add(new MailboxAddress(account.SenderEmail ?? account.Username, account.SenderEmail ?? account.Username));
                emailMessage.To.Add(new MailboxAddress(email, email));
                emailMessage.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = message
                };

                bodyBuilder.Attachments.Add(attachmentFilename, attachmentBytes, ContentType.Parse(attachmentContentType));
                emailMessage.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    client.Timeout = 60000;

                    bool connected = false;
                    Exception? lastException = null;

                    var connectionModes = new[]
                    {
                        (SecureSocketOptions.SslOnConnect, "SSL/TLS"),
                        (SecureSocketOptions.StartTls, "STARTTLS"),
                        (SecureSocketOptions.Auto, "Auto"),
                        (SecureSocketOptions.None, "Plain (no encryption)")
                    };

                    foreach (var (mode, modeName) in connectionModes)
                    {
                        try
                        {
                            _logger.LogInformation("Attempting {Mode} connection to {Server}:{Port}",
                                modeName, _websiteSettings.EmailServer.SmtpAddress, _websiteSettings.EmailServer.SmtpPort);

                            await client.ConnectAsync(
                                _websiteSettings.EmailServer.SmtpAddress,
                                _websiteSettings.EmailServer.SmtpPort,
                                mode);

                            connected = true;
                            _logger.LogInformation("Successfully connected using {Mode}", modeName);
                            break;
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            _logger.LogWarning(ex, "{Mode} connection failed: {Message}", modeName, ex.Message);
                        }
                    }

                    if (!connected)
                    {
                        throw new InvalidOperationException(
                            $"Failed to connect to SMTP server after trying all connection modes. Last error: {lastException?.Message}",
                            lastException);
                    }

                    _logger.LogInformation("Authenticating as {Username}", account.Username);
                    await client.AuthenticateAsync(account.Username, account.Password);
                    _logger.LogInformation("Authentication successful");

                    _logger.LogInformation("Sending email with attachment...");
                    await client.SendAsync(emailMessage);
                    _logger.LogInformation("Email with attachment sent successfully");

                    await client.DisconnectAsync(true);

                    _logger.LogInformation("Successfully sent email with attachment to: {Email}", email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email with attachment to {Email}. Error type: {ErrorType}, Message: {Message}",
                    email, ex.GetType().Name, ex.Message);
                throw;
            }
        }
    }
}
