using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: purchase-classification-enhancements, Property 1: Origin Type Validation Range

/// <summary>
/// Property-based tests for PurchaseService validation of PurchaseOriginTypeId range.
/// For any PurchaseOriginTypeId in {1,2,3,4} validation accepts; for any value outside that set, validation rejects.
/// **Validates: Requirements 6.1**
/// </summary>
public class PurchaseOriginTypeValidationRangePropertyTests
{
    private const int TestBusinessId = 1;
    private const int TestSupplierId = 10;
    private const int TestExpenseCategoryId = 5;

    /// <summary>
    /// Creates a PurchaseService with mocked dependencies configured for origin type validation testing.
    /// Supplier and ExpenseCategory are always valid so we isolate origin type range checks.
    /// </summary>
    private static PurchaseService CreateServiceWithMocks()
    {
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var mockSupplierRepo = new Mock<SupplierRepository>(MockBehavior.Strict, new object[] { null! });
        mockSupplierRepo
            .Setup(r => r.GetByIdAndBusinessIdAsync(TestSupplierId, TestBusinessId))
            .ReturnsAsync(new Supplier
            {
                Id = TestSupplierId,
                BusinessId = TestBusinessId,
                Name = "Test Supplier",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

        var mockExpenseCategoryRepo = new Mock<ExpenseCategoryRepository>(MockBehavior.Strict, new object[] { null! });
        mockExpenseCategoryRepo
            .Setup(r => r.GetByIdAndBusinessIdAsync(TestExpenseCategoryId, TestBusinessId))
            .ReturnsAsync(new ExpenseCategory
            {
                Id = TestExpenseCategoryId,
                BusinessId = TestBusinessId,
                Name = "Test Category",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

        var mockPurchaseRepo = new Mock<PurchaseRepository>(MockBehavior.Loose, new object[] { null! });
        mockPurchaseRepo
            .Setup(r => r.InsertAsync(It.IsAny<Purchase>()))
            .Returns(Task.CompletedTask);

        var mockAuditLogRepo = new Mock<AuditLogRepository>(MockBehavior.Loose, new object[] { null! });
        mockAuditLogRepo
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        // Create an in-memory DbContext for VatSubmissionPeriod queries
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"OriginTypeValidationTest_{Guid.NewGuid()}")
            .Options;

        var dbContext = new PortalDbContext(options, mockTenantService.Object);

        var service = new PurchaseService(
            mockTenantService.Object,
            mockPurchaseRepo.Object,
            mockSupplierRepo.Object,
            mockExpenseCategoryRepo.Object,
            mockAuditLogRepo.Object,
            dbContext);

        return service;
    }

    /// <summary>
    /// Creates a valid purchase entity with the specified origin type ID.
    /// All other fields are set to valid values so only origin type validation is tested.
    /// Country is set to a non-empty value to satisfy the Country requirement for origin types 2, 3, 4.
    /// </summary>
    private static Purchase CreateValidPurchase(int originTypeId)
    {
        return new Purchase
        {
            SupplierId = TestSupplierId,
            ExpenseCategoryId = TestExpenseCategoryId,
            PurchaseOriginTypeId = originTypeId,
            PurchaseTypeId = 3, // Expense (valid)
            InvoiceNumber = "INV-001",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = "Test purchase",
            AmountExcludingVat = 100.00m,
            VatAmount = 15.00m,
            Country = "Germany" // Non-empty to satisfy Country requirement for EU types
        };
    }

    #region Property 1a: Valid origin type IDs (1–4) are accepted

    /// <summary>
    /// Property 1a: For any PurchaseOriginTypeId in {1, 2, 3, 4} and otherwise valid fields,
    /// the service validation SHALL accept the purchase.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidOriginTypeId_IsAccepted(PositiveInt seed)
    {
        // Map seed to one of the valid origin type IDs: 1, 2, 3, 4
        var validIds = new[] { 1, 2, 3, 4 };
        var originTypeId = validIds[seed.Get % validIds.Length];

        var service = CreateServiceWithMocks();
        var purchase = CreateValidPurchase(originTypeId);

        var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

        return result.Success.ToProperty()
            .Label($"OriginTypeId={originTypeId}: Success={result.Success}, Message='{result.Message}'");
    }

    #endregion

    #region Property 1b: Invalid origin type IDs (outside 1–4) are rejected

    /// <summary>
    /// Property 1b: For any PurchaseOriginTypeId value outside {1, 2, 3, 4} (e.g., 0, 5, -1, 99),
    /// the service validation SHALL reject the purchase with "Invalid purchase origin type."
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidOriginTypeId_IsRejected(int rawId)
    {
        // Skip valid IDs — we only want to test invalid ones
        if (rawId >= 1 && rawId <= 4)
            return true.ToProperty().Label("Skipped (valid ID)");

        var service = CreateServiceWithMocks();
        var purchase = CreateValidPurchase(rawId);

        var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

        var isRejected = !result.Success;
        var hasCorrectMessage = result.Message == "Invalid purchase origin type.";

        return (isRejected && hasCorrectMessage).ToProperty()
            .Label($"OriginTypeId={rawId}: Success={result.Success}, Message='{result.Message}'");
    }

    #endregion
}
