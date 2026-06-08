using FsCheck;
using FsCheck.Xunit;
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

namespace Portal.Tests.PropertyBased.Billing;

// Feature: subscription-billing-invoices, Property 6: No duplicate emails per invoice

/// <summary>
/// Property-based tests for InvoiceEmailService idempotency.
/// Verifies that for any BillingInvoice record, calling SendInvoiceNotificationAsync
/// multiple times results in at most one email being sent — subsequent calls are no-ops
/// when IsEmailSent is already true.
/// **Validates: Requirements 6.7**
/// </summary>
public class InvoiceEmailIdempotencyPropertyTests
{
    #region Generators

    /// <summary>
    /// Holds the parameters for a single property test case.
    /// </summary>
    private record TestInput(int InvoiceId, int BusinessId, decimal Amount, int CallCount, string InvoiceNumber);

    /// <summary>
    /// Generates a complete TestInput with random values for all parameters.
    /// </summary>
    private static Gen<TestInput> TestInputGen =>
        from invoiceId in Gen.Choose(1, 10000)
        from businessId in Gen.Choose(1, 1000)
        from amountCents in Gen.Choose(100, 100000)
        from callCount in Gen.Choose(2, 5)
        from year in Gen.Choose(2020, 2030)
        from seq in Gen.Choose(1, 9999)
        select new TestInput(invoiceId, businessId, (decimal)amountCents / 100m, callCount, $"BILI-INV-{year}-{seq:D4}");

    #endregion

    /// <summary>
    /// For any BillingInvoice record, calling SendInvoiceNotificationAsync multiple times
    /// SHALL result in at most one email being sent — subsequent calls SHALL be no-ops
    /// when IsEmailSent is already true.
    /// **Validates: Requirements 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SendInvoiceNotificationAsync_MultipleCallsResultInAtMostOneEmail()
    {
        return Prop.ForAll(
            TestInputGen.ToArbitrary(),
            (input) =>
            {
                var (invoiceId, businessId, amount, callCount, invoiceNumber) = input;

                // Arrange: create in-memory PortalDbContext
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(businessId);

                var portalDbOptions = new DbContextOptionsBuilder<PortalDbContext>()
                    .UseInMemoryDatabase(databaseName: $"EmailIdempotency_{Guid.NewGuid()}")
                    .Options;
                var portalDbContext = new PortalDbContext(portalDbOptions, tenantMock.Object);

                // Seed a BillingInvoice with IsEmailSent = false
                var invoice = new BillingInvoice
                {
                    Id = invoiceId,
                    BusinessId = businessId,
                    AmountEur = amount,
                    InvoiceNumber = invoiceNumber,
                    IsEmailSent = false,
                    Status = "paid",
                    PeriodStart = new DateTime(2025, 1, 1),
                    PeriodEnd = new DateTime(2025, 1, 31),
                    CreatedAtUtc = DateTime.UtcNow
                };
                portalDbContext.BillingInvoices.Add(invoice);
                portalDbContext.SaveChanges();

                // Create in-memory MembershipDbContext with a business owner
                var membershipDbOptions = new DbContextOptionsBuilder<MembershipDbContext>()
                    .UseInMemoryDatabase(databaseName: $"EmailIdempotencyMembership_{Guid.NewGuid()}")
                    .Options;
                var membershipDbContext = new MembershipDbContext(membershipDbOptions);

                var userId = Guid.NewGuid().ToString();
                var user = new ApplicationUser
                {
                    Id = userId,
                    Email = "owner@example.com",
                    UserName = "owner@example.com",
                    FirstName = "Test",
                    LastName = "Owner",
                    NormalizedUserName = "OWNER@EXAMPLE.COM",
                    NormalizedEmail = "OWNER@EXAMPLE.COM"
                };
                membershipDbContext.Users.Add(user);

                var userBusiness = new UserBusiness
                {
                    Id = invoiceId, // Use invoiceId as a unique key
                    UserId = userId,
                    BusinessId = businessId,
                    IsOwner = true,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                membershipDbContext.UserBusinesses.Add(userBusiness);
                membershipDbContext.SaveChanges();

                // Mock email sender — track how many times SendEmailAsync is called
                var emailSenderMock = new Mock<IEmailSender>();
                var sendEmailCallCount = 0;
                emailSenderMock
                    .Setup(e => e.SendEmailAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<EmailDepartmentEnum>()))
                    .Callback(() => Interlocked.Increment(ref sendEmailCallCount))
                    .Returns(Task.CompletedTask);

                // Mock HttpContextAccessor
                var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
                var httpContext = new DefaultHttpContext();
                httpContext.Request.Scheme = "https";
                httpContext.Request.Host = new HostString("portal.example.com");
                httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

                // Mock Logger
                var loggerMock = new Mock<ILogger<InvoiceEmailService>>();

                // Create the service under test
                var service = new InvoiceEmailService(
                    portalDbContext,
                    membershipDbContext,
                    emailSenderMock.Object,
                    httpContextAccessorMock.Object,
                    loggerMock.Object);

                // Act: call SendInvoiceNotificationAsync multiple times
                for (int i = 0; i < callCount; i++)
                {
                    service.SendInvoiceNotificationAsync(invoiceId).GetAwaiter().GetResult();
                }

                // Assert: email was sent at most once
                var emailSentExactlyOnce = sendEmailCallCount == 1;

                // Cleanup
                portalDbContext.Database.EnsureDeleted();
                portalDbContext.Dispose();
                membershipDbContext.Database.EnsureDeleted();
                membershipDbContext.Dispose();

                return emailSentExactlyOnce
                    .Label($"Expected email to be sent exactly once, but was sent {sendEmailCallCount} times after {callCount} calls");
            });
    }

    /// <summary>
    /// For any BillingInvoice record that already has IsEmailSent = true,
    /// calling SendInvoiceNotificationAsync SHALL not invoke the email sender at all.
    /// **Validates: Requirements 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SendInvoiceNotificationAsync_NoEmailWhenAlreadySent()
    {
        return Prop.ForAll(
            TestInputGen.ToArbitrary(),
            (input) =>
            {
                var (invoiceId, businessId, amount, callCount, invoiceNumber) = input;

                // Arrange: create in-memory PortalDbContext
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(businessId);

                var portalDbOptions = new DbContextOptionsBuilder<PortalDbContext>()
                    .UseInMemoryDatabase(databaseName: $"EmailAlreadySent_{Guid.NewGuid()}")
                    .Options;
                var portalDbContext = new PortalDbContext(portalDbOptions, tenantMock.Object);

                // Seed a BillingInvoice with IsEmailSent = true (already sent)
                var invoice = new BillingInvoice
                {
                    Id = invoiceId,
                    BusinessId = businessId,
                    AmountEur = amount,
                    InvoiceNumber = invoiceNumber,
                    IsEmailSent = true, // Already sent
                    Status = "paid",
                    PeriodStart = new DateTime(2025, 1, 1),
                    PeriodEnd = new DateTime(2025, 1, 31),
                    CreatedAtUtc = DateTime.UtcNow
                };
                portalDbContext.BillingInvoices.Add(invoice);
                portalDbContext.SaveChanges();

                // MembershipDbContext not strictly needed since IsEmailSent check short-circuits,
                // but create one for completeness
                var membershipDbOptions = new DbContextOptionsBuilder<MembershipDbContext>()
                    .UseInMemoryDatabase(databaseName: $"EmailAlreadySentMembership_{Guid.NewGuid()}")
                    .Options;
                var membershipDbContext = new MembershipDbContext(membershipDbOptions);

                // Mock email sender
                var emailSenderMock = new Mock<IEmailSender>();
                var sendEmailCallCount = 0;
                emailSenderMock
                    .Setup(e => e.SendEmailAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<EmailDepartmentEnum>()))
                    .Callback(() => Interlocked.Increment(ref sendEmailCallCount))
                    .Returns(Task.CompletedTask);

                // Mock HttpContextAccessor
                var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
                var httpContext = new DefaultHttpContext();
                httpContext.Request.Scheme = "https";
                httpContext.Request.Host = new HostString("portal.example.com");
                httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

                // Mock Logger
                var loggerMock = new Mock<ILogger<InvoiceEmailService>>();

                // Create the service under test
                var service = new InvoiceEmailService(
                    portalDbContext,
                    membershipDbContext,
                    emailSenderMock.Object,
                    httpContextAccessorMock.Object,
                    loggerMock.Object);

                // Act: call multiple times
                for (int i = 0; i < callCount; i++)
                {
                    service.SendInvoiceNotificationAsync(invoiceId).GetAwaiter().GetResult();
                }

                // Assert: email was never sent
                var emailNeverSent = sendEmailCallCount == 0;

                // Cleanup
                portalDbContext.Database.EnsureDeleted();
                portalDbContext.Dispose();
                membershipDbContext.Database.EnsureDeleted();
                membershipDbContext.Dispose();

                return emailNeverSent
                    .Label($"Expected no emails to be sent (IsEmailSent was already true), but {sendEmailCallCount} emails were sent");
            });
    }
}
