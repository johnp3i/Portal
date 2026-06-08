using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Services;
using Portal.Web.Services.Billing;
using Portal.Web.Services.Email;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for InvoiceEmailService covering email delivery logic,
/// duplicate prevention, missing email handling, and SMTP failure resilience.
/// Validates Requirements 6.3, 6.4, 6.6, 6.7.
/// </summary>
public class InvoiceEmailServiceTests : IDisposable
{
    private const int TestBusinessId = 1;
    private const int TestInvoiceId = 42;
    private const string TestInvoiceNumber = "BILI-INV-2026-0001";
    private const string TestOwnerEmail = "owner@example.com";

    private readonly PortalDbContext _portalDbContext;
    private readonly MembershipDbContext _membershipDbContext;
    private readonly Mock<IEmailSender> _emailSenderMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<ILogger<InvoiceEmailService>> _loggerMock;
    private readonly InvoiceEmailService _service;

    public InvoiceEmailServiceTests()
    {
        // Set up PortalDbContext with InMemory database
        var tenantServiceMock = new Mock<ICurrentTenantService>();
        tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var portalOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"Portal_InvoiceEmail_{Guid.NewGuid()}")
            .Options;

        _portalDbContext = new PortalDbContext(portalOptions, tenantServiceMock.Object);

        // Set up MembershipDbContext with InMemory database
        var membershipOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"Membership_InvoiceEmail_{Guid.NewGuid()}")
            .Options;

        _membershipDbContext = new MembershipDbContext(membershipOptions);

        // Set up mocks
        _emailSenderMock = new Mock<IEmailSender>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _loggerMock = new Mock<ILogger<InvoiceEmailService>>();

        // Set up a basic HttpContext for URL generation
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("portal.3inventors.com");
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        _service = new InvoiceEmailService(
            _portalDbContext,
            _membershipDbContext,
            _emailSenderMock.Object,
            _httpContextAccessorMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _portalDbContext.Database.EnsureDeleted();
        _portalDbContext.Dispose();
        _membershipDbContext.Database.EnsureDeleted();
        _membershipDbContext.Dispose();
    }

    #region Helpers

    private void SeedBillingInvoice(int id = TestInvoiceId, bool isEmailSent = false, string? invoiceNumber = TestInvoiceNumber)
    {
        _portalDbContext.BillingInvoices.Add(new BillingInvoice
        {
            Id = id,
            BusinessId = TestBusinessId,
            StripeInvoiceId = "si_test_123",
            AmountEur = 49.99m,
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 1, 31),
            Status = "paid",
            PaidAtUtc = DateTime.UtcNow,
            InvoiceNumber = invoiceNumber,
            IsEmailSent = isEmailSent,
            CreatedAtUtc = DateTime.UtcNow
        });
        _portalDbContext.SaveChanges();
    }

    private void SeedBusinessOwnerWithEmail(string email = TestOwnerEmail)
    {
        var user = new ApplicationUser
        {
            Id = "user-owner-001",
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Test",
            LastName = "Owner",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _membershipDbContext.Users.Add(user);

        _membershipDbContext.UserBusinesses.Add(new UserBusiness
        {
            Id = 1,
            UserId = user.Id,
            BusinessId = TestBusinessId,
            IsOwner = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        _membershipDbContext.SaveChanges();
    }

    private void SeedBusinessOwnerWithNoEmail()
    {
        var user = new ApplicationUser
        {
            Id = "user-owner-002",
            UserName = "noemail",
            NormalizedUserName = "NOEMAIL",
            Email = null,
            NormalizedEmail = null,
            FirstName = "No",
            LastName = "Email",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _membershipDbContext.Users.Add(user);

        _membershipDbContext.UserBusinesses.Add(new UserBusiness
        {
            Id = 2,
            UserId = user.Id,
            BusinessId = TestBusinessId,
            IsOwner = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        _membershipDbContext.SaveChanges();
    }

    #endregion

    #region Email sent on first call with correct department (Req 6.3)

    [Fact]
    public async Task SendInvoiceNotificationAsync_FirstCall_SendsEmailWithInvoicesDepartment()
    {
        // Arrange
        SeedBillingInvoice(isEmailSent: false);
        SeedBusinessOwnerWithEmail();

        _emailSenderMock
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailDepartmentEnum>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendInvoiceNotificationAsync(TestInvoiceId);

        // Assert — email sent exactly once with EmailDepartmentEnum.Invoices
        _emailSenderMock.Verify(
            e => e.SendEmailAsync(
                TestOwnerEmail,
                It.Is<string>(s => s.Contains(TestInvoiceNumber)),
                It.IsAny<string>(),
                EmailDepartmentEnum.Invoices),
            Times.Once);
    }

    [Fact]
    public async Task SendInvoiceNotificationAsync_FirstCall_MarksIsEmailSentTrue()
    {
        // Arrange
        SeedBillingInvoice(isEmailSent: false);
        SeedBusinessOwnerWithEmail();

        _emailSenderMock
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailDepartmentEnum>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendInvoiceNotificationAsync(TestInvoiceId);

        // Assert — IsEmailSent flag updated to true
        var invoice = await _portalDbContext.BillingInvoices.FindAsync(TestInvoiceId);
        Assert.True(invoice!.IsEmailSent);
    }

    #endregion

    #region No email sent when IsEmailSent is already true (Req 6.7)

    [Fact]
    public async Task SendInvoiceNotificationAsync_AlreadySent_DoesNotSendEmail()
    {
        // Arrange — invoice already has IsEmailSent = true
        SeedBillingInvoice(isEmailSent: true);
        SeedBusinessOwnerWithEmail();

        // Act
        await _service.SendInvoiceNotificationAsync(TestInvoiceId);

        // Assert — email sender is never called
        _emailSenderMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailDepartmentEnum>()),
            Times.Never);
    }

    #endregion

    #region No email sent when business has no email address (Req 6.6)

    [Fact]
    public async Task SendInvoiceNotificationAsync_NoOwnerEmail_DoesNotSendAndLogsWarning()
    {
        // Arrange — owner has no email address
        SeedBillingInvoice(isEmailSent: false);
        SeedBusinessOwnerWithNoEmail();

        // Act
        await _service.SendInvoiceNotificationAsync(TestInvoiceId);

        // Assert — email sender is never called
        _emailSenderMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailDepartmentEnum>()),
            Times.Never);

        // Assert — warning logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No email address found")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SMTP failure logged as warning, no exception thrown (Req 6.4)

    [Fact]
    public async Task SendInvoiceNotificationAsync_SmtpFailure_LogsWarningAndDoesNotThrow()
    {
        // Arrange
        SeedBillingInvoice(isEmailSent: false);
        SeedBusinessOwnerWithEmail();

        _emailSenderMock
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailDepartmentEnum>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        // Act — should NOT throw
        var exception = await Record.ExceptionAsync(() => _service.SendInvoiceNotificationAsync(TestInvoiceId));

        // Assert — no exception propagated
        Assert.Null(exception);

        // Assert — warning logged with the exception
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send invoice notification")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendInvoiceNotificationAsync_SmtpFailure_DoesNotMarkIsEmailSent()
    {
        // Arrange
        SeedBillingInvoice(isEmailSent: false);
        SeedBusinessOwnerWithEmail();

        _emailSenderMock
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailDepartmentEnum>()))
            .ThrowsAsync(new InvalidOperationException("SMTP timeout"));

        // Act
        await _service.SendInvoiceNotificationAsync(TestInvoiceId);

        // Assert — IsEmailSent remains false since the email wasn't actually delivered
        var invoice = await _portalDbContext.BillingInvoices.FindAsync(TestInvoiceId);
        Assert.False(invoice!.IsEmailSent);
    }

    #endregion
}
