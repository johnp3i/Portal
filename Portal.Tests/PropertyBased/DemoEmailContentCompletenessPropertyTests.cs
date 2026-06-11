using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Services;
using Portal.Web.Services;
using Portal.Web.Services.Email;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: demo-access-invitations, Property 7: Email content

/// <summary>
/// Property-based tests for demo invitation email content completeness.
/// Verifies that for any business name and expiry date, the generated HTML contains
/// the business name, the expiry date in human-readable format, and an anchor element
/// whose href contains the magic link URL.
/// **Validates: Requirements 6.2**
/// </summary>
public class DemoEmailContentCompletenessPropertyTests
{
    /// <summary>
    /// Creates a PortalEmailService with a mocked IEmailSender that captures the HTML body.
    /// Returns the service and a function to retrieve the captured HTML.
    /// </summary>
    private static (PortalEmailService service, Func<string?> getCapturedHtml) CreateServiceWithCapture()
    {
        string? capturedHtml = null;

        var mockEmailSender = new Mock<IEmailSender>();
        mockEmailSender
            .Setup(s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<EmailDepartmentEnum>()))
            .Callback<string, string, string, EmailDepartmentEnum>((_, _, html, _) =>
            {
                capturedHtml = html;
            })
            .Returns(Task.CompletedTask);

        var mockLogger = new Mock<ILogger<PortalEmailService>>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        var service = new PortalEmailService(
            mockEmailSender.Object,
            mockLogger.Object,
            mockHttpContextAccessor.Object);

        return (service, () => capturedHtml);
    }

    /// <summary>
    /// Property 7a: The generated email HTML contains the business name.
    /// For any non-empty business name, it appears (HTML-encoded) in the email body.
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmailHtml_ContainsBusinessName()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString().Generator
                .Where(s => !string.IsNullOrWhiteSpace(s.Get))
                .ToArbitrary(),
            businessNameNes =>
            {
                var businessName = businessNameNes.Get;
                var (service, getCapturedHtml) = CreateServiceWithCapture();
                var magicLink = "https://portal.example.com/Demo/Enter?token=test-token-123";
                var expiresAtUtc = DateTime.UtcNow.AddDays(7);

                service.SendDemoInvitationEmailAsync("test@example.com", magicLink, businessName, expiresAtUtc)
                    .GetAwaiter().GetResult();

                var html = getCapturedHtml();
                var encodedName = System.Net.WebUtility.HtmlEncode(businessName);

                return (html != null && html.Contains(encodedName)).ToProperty()
                    .Label($"Email HTML should contain business name '{encodedName}'");
            });
    }

    /// <summary>
    /// Property 7b: The generated email HTML contains the expiry date in human-readable format.
    /// The format used is "dd MMMM yyyy 'at' HH:mm 'UTC'" as per implementation.
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmailHtml_ContainsExpiryDateInReadableFormat()
    {
        return Prop.ForAll(
            Arb.Default.DateTime().Generator
                .Select(dt => DateTime.SpecifyKind(
                    new DateTime(
                        Math.Clamp(dt.Year, 2020, 2099),
                        Math.Clamp(dt.Month, 1, 12),
                        Math.Clamp(dt.Day, 1, 28),
                        Math.Clamp(dt.Hour, 0, 23),
                        Math.Clamp(dt.Minute, 0, 59),
                        0),
                    DateTimeKind.Utc))
                .ToArbitrary(),
            expiresAtUtc =>
            {
                var (service, getCapturedHtml) = CreateServiceWithCapture();
                var businessName = "Test Business";
                var magicLink = "https://portal.example.com/Demo/Enter?token=abc123";

                service.SendDemoInvitationEmailAsync("test@example.com", magicLink, businessName, expiresAtUtc)
                    .GetAwaiter().GetResult();

                var html = getCapturedHtml();
                var expectedDateString = expiresAtUtc.ToString("dd MMMM yyyy 'at' HH:mm 'UTC'");

                return (html != null && html.Contains(expectedDateString)).ToProperty()
                    .Label($"Email HTML should contain expiry date '{expectedDateString}'");
            });
    }

    /// <summary>
    /// Property 7c: The generated email HTML contains an anchor element whose href
    /// contains the magic link URL.
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmailHtml_ContainsAnchorWithMagicLinkHref()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString().Generator
                .Select(s => $"https://portal.example.com/Demo/Enter?token={s.Get.Replace("\"", "").Replace("<", "").Replace(">", "").Replace("&", "")}")
                .Where(url => url.Length > 50 && url.Length < 500)
                .ToArbitrary(),
            magicLink =>
            {
                var (service, getCapturedHtml) = CreateServiceWithCapture();
                var businessName = "Demo Corp";
                var expiresAtUtc = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);

                service.SendDemoInvitationEmailAsync("test@example.com", magicLink, businessName, expiresAtUtc)
                    .GetAwaiter().GetResult();

                var html = getCapturedHtml();
                var encodedLink = System.Net.WebUtility.HtmlEncode(magicLink);

                // Verify the HTML contains an anchor tag with the magic link in href
                var containsAnchorWithHref = html != null &&
                    html.Contains($"href=\"{encodedLink}\"", StringComparison.OrdinalIgnoreCase);

                return containsAnchorWithHref.ToProperty()
                    .Label($"Email HTML should contain <a href=\"{encodedLink}\">");
            });
    }
}
