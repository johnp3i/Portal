using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Portal.Tests.Integration;

/// <summary>
/// End-to-end integration test for Payment Reminders module.
/// Validates: schedule -> evaluate -> send -> log -> idempotency.
/// Satisfies: Phase 1 timetable task 3.16
/// </summary>
public class PaymentReminderEndToEndTests
{
    private const int TestBusinessId = 1;
    private const int TestCustomerId = 1;
    private const int TestInvoiceId = 1;

    [Fact]
    public async Task EvaluateAndSend_WithEligibleInvoice_CreatesLogEntry()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithSeededData();
        try
        {
            var evaluationDate = DateOnly.FromDateTime(DateTime.UtcNow);

            // Act
            var result = await service.EvaluateAndSendAsync(TestBusinessId, evaluationDate);

            // Assert
            Assert.Equal(1, result.InvoicesEvaluated);
            Assert.Equal(1, result.RemindersSent);
            Assert.Equal(0, result.RemindersFailed);

            // Verify log entry was created
            var logs = await dbContext.PaymentReminderLogs.ToListAsync();
            Assert.Single(logs);
            Assert.Equal(TestBusinessId, logs[0].BusinessId);
            Assert.Equal(TestInvoiceId, logs[0].InvoiceId);
            Assert.Equal(TestCustomerId, logs[0].CustomerId);
            Assert.Equal("Friendly", logs[0].EscalationTier);
            Assert.True(logs[0].IsSentSuccessfully);
            Assert.False(logs[0].IsManualTrigger);
            Assert.False(logs[0].IsTestSend);
            Assert.NotNull(logs[0].TrackingToken);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    [Fact]
    public async Task EvaluateAndSend_SecondRunSameDate_IsIdempotent()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithSeededData();
        try
        {
            var evaluationDate = DateOnly.FromDateTime(DateTime.UtcNow);

            // Act - first run
            await service.EvaluateAndSendAsync(TestBusinessId, evaluationDate);

            // Act - second run (same date)
            var result2 = await service.EvaluateAndSendAsync(TestBusinessId, evaluationDate);

            // Assert - no duplicates
            Assert.Equal(0, result2.RemindersSent);
            var logs = await dbContext.PaymentReminderLogs.ToListAsync();
            Assert.Single(logs); // Still only 1 log entry
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    [Fact]
    public async Task EvaluateAndSend_CustomerOptedOut_SkipsInvoice()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithSeededData(customerOptedOut: true);
        try
        {
            var evaluationDate = DateOnly.FromDateTime(DateTime.UtcNow);

            // Act
            var result = await service.EvaluateAndSendAsync(TestBusinessId, evaluationDate);

            // Assert
            Assert.Equal(0, result.RemindersSent);
            var logs = await dbContext.PaymentReminderLogs.ToListAsync();
            Assert.Empty(logs);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    [Fact]
    public async Task EvaluateAndSend_DisputedInvoice_SkipsInvoice()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithSeededData(invoiceDisputed: true);
        try
        {
            var evaluationDate = DateOnly.FromDateTime(DateTime.UtcNow);

            // Act
            var result = await service.EvaluateAndSendAsync(TestBusinessId, evaluationDate);

            // Assert
            Assert.Equal(0, result.RemindersSent);
            var logs = await dbContext.PaymentReminderLogs.ToListAsync();
            Assert.Empty(logs);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    [Fact]
    public async Task EvaluateAndSend_TestSendExcludedFromCaps()
    {
        // Arrange
        var (service, dbContext) = CreateServiceWithSeededData();
        try
        {
            var evaluationDate = DateOnly.FromDateTime(DateTime.UtcNow);

            // Add a test send log (should NOT count toward max cap)
            dbContext.PaymentReminderLogs.Add(new PaymentReminderLog
            {
                BusinessId = TestBusinessId,
                InvoiceId = TestInvoiceId,
                CustomerId = TestCustomerId,
                RecipientEmail = "test@test.com",
                EscalationTier = "Friendly",
                IsSentSuccessfully = true,
                IsManualTrigger = true,
                IsTestSend = true, // This is a test send
                SentAtUtc = DateTime.UtcNow.AddHours(-1),
                TrackingToken = "test-token-123"
            });
            await dbContext.SaveChangesAsync();

            // Act - should still send because test sends don't count toward max cap
            var result = await service.EvaluateAndSendAsync(TestBusinessId, evaluationDate);

            // Assert
            Assert.Equal(1, result.RemindersSent);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    /// <summary>
    /// Creates a PaymentReminderService with an in-memory database seeded with test data.
    /// The invoice is set up to trigger the Friendly tier on today's evaluation date.
    /// </summary>
    private (PaymentReminderService service, PortalDbContext dbContext) CreateServiceWithSeededData(
        bool customerOptedOut = false,
        bool invoiceDisputed = false)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"ReminderE2E_{Guid.NewGuid()}")
            .Options;

        var dbContext = new PortalDbContext(options, tenantMock.Object);

        // Seed business
        dbContext.Businesses.Add(new Business
        {
            Id = TestBusinessId,
            Name = "Test Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        // Seed customer
        dbContext.Customers.Add(new Customer
        {
            Id = TestCustomerId,
            BusinessId = TestBusinessId,
            Name = "Test Customer",
            Email = "customer@test.com",
            IsActive = true,
            IsReminderOptedOut = customerOptedOut,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        // Seed invoice - due date set so that DueDate + FriendlyOffset(-3) = today
        var today = DateTime.UtcNow.Date;
        dbContext.Invoices.Add(new Invoice
        {
            Id = TestInvoiceId,
            BusinessId = TestBusinessId,
            CustomerId = TestCustomerId,
            InvoiceNumber = "INV-TEST-001",
            InvoiceStatusTypeId = 2, // Issued
            InvoiceFinancialStatusTypeId = 1, // Unpaid
            DueDate = DateOnly.FromDateTime(today.AddDays(3)), // DueDate + (-3 offset) = today
            InvoiceDate = DateOnly.FromDateTime(today.AddDays(-7)),
            Subtotal = 1000m,
            TaxAmount = 180m,
            TotalAmount = 1180m,
            CurrencyCode = "EUR",
            IsDeleted = false,
            IsDisputed = invoiceDisputed,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        // Seed reminder schedule (Friendly tier, -3 days offset, enabled)
        dbContext.PaymentReminderSchedules.Add(new PaymentReminderSchedule
        {
            BusinessId = TestBusinessId,
            EscalationTier = "Friendly",
            DaysOffset = -3,
            MaxRemindersPerTier = 1,
            MinIntervalDays = 3,
            PartialPaymentSuppressionDays = 7,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        // Seed plan and feature for module eligibility
        dbContext.Plans.Add(new Plan
        {
            Id = 1,
            Name = "Professional",
            Slug = "professional",
            MonthlyPriceEur = 79,
            MaxUsers = 10,
            IsActive = true,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        dbContext.PlanFeatures.Add(new PlanFeature
        {
            PlanId = 1,
            ModuleName = "payment_reminder_auto",
            IsIncluded = true,
            AccessLevel = "full",
            CreatedAtUtc = DateTime.UtcNow
        });

        dbContext.BusinessPlans.Add(new BusinessPlan
        {
            BusinessId = TestBusinessId,
            PlanId = 1,
            IsActive = true,
            Status = "active",
            StartDateUtc = DateTime.UtcNow.AddMonths(-1),
            CreatedAtUtc = DateTime.UtcNow
        });

        dbContext.SaveChanges();

        // Mock email service (always succeeds)
        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock
            .Setup(e => e.SendPaymentReminderEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<DateOnly>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        // Schedule service uses the same in-memory context (real implementation)
        var scheduleService = new PaymentReminderScheduleService(dbContext);

        // Mock HttpContextAccessor
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        // Mock IInvoiceSharingService (not needed for evaluation logic)
        var sharingServiceMock = new Mock<IInvoiceSharingService>();

        var service = new PaymentReminderService(
            dbContext,
            scheduleService,
            emailServiceMock.Object,
            httpContextAccessorMock.Object,
            sharingServiceMock.Object);

        return (service, dbContext);
    }
}
