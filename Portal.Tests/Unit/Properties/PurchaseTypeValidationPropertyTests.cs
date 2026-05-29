using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for PurchaseTypeId validation range.
/// Tests Property 6 from the purchase-classification-enhancements design document.
/// </summary>
public class PurchaseTypeValidationPropertyTests
{
    private const int TestBusinessId = 1;
    private const int TestSupplierId = 10;
    private const int TestExpenseCategoryId = 20;

    #region Shared Mock Setup

    private static PurchaseService CreatePurchaseService(
        Mock<ICurrentTenantService> tenantMock,
        Mock<SupplierRepository> supplierRepoMock,
        Mock<ExpenseCategoryRepository> expenseCategoryRepoMock,
        Mock<PurchaseRepository> purchaseRepoMock,
        Mock<AuditLogRepository> auditLogRepoMock,
        Mock<PortalDbContext> dbContextMock)
    {
        return new PurchaseService(
            tenantMock.Object,
            purchaseRepoMock.Object,
            supplierRepoMock.Object,
            expenseCategoryRepoMock.Object,
            auditLogRepoMock.Object,
            dbContextMock.Object);
    }

    private static (Mock<ICurrentTenantService>, Mock<SupplierRepository>, Mock<ExpenseCategoryRepository>, Mock<PurchaseRepository>, Mock<AuditLogRepository>, Mock<PortalDbContext>) CreatePurchaseMocks()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContextMock = new Mock<PortalDbContext>(dbContextOptions, tenantMock.Object) { CallBase = true };

        var supplierRepoMock = new Mock<SupplierRepository>(dbContextMock.Object) { CallBase = false };
        supplierRepoMock
            .Setup(r => r.GetByIdAndBusinessIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new Supplier { Id = TestSupplierId, BusinessId = TestBusinessId, Name = "Test Supplier", IsActive = true });

        var expenseCategoryRepoMock = new Mock<ExpenseCategoryRepository>(dbContextMock.Object) { CallBase = false };
        expenseCategoryRepoMock
            .Setup(r => r.GetByIdAndBusinessIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new ExpenseCategory { Id = TestExpenseCategoryId, BusinessId = TestBusinessId, Name = "Test Category", IsActive = true });

        var purchaseRepoMock = new Mock<PurchaseRepository>(dbContextMock.Object) { CallBase = false };
        purchaseRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<Purchase>()))
            .Returns(Task.CompletedTask);

        var auditLogRepoMock = new Mock<AuditLogRepository>(dbContextMock.Object) { CallBase = false };
        auditLogRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        return (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);
    }

    private static Purchase CreateValidPurchase(int purchaseTypeId)
    {
        return new Purchase
        {
            SupplierId = TestSupplierId,
            ExpenseCategoryId = TestExpenseCategoryId,
            PurchaseOriginTypeId = 1, // Domestic — no Country required
            PurchaseTypeId = purchaseTypeId,
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            Description = "Test purchase",
            AmountExcludingVat = 100.00m,
            VatAmount = 15.00m,
            InvoiceNumber = "INV-001"
        };
    }

    #endregion

    #region Property 6: PurchaseTypeId Validation Range

    // Feature: purchase-classification-enhancements, Property 6: PurchaseTypeId Validation Range
    // **Validates: Requirements 3.3, 3.4, 6.4**
    [Property(MaxTest = 100)]
    public Property PurchaseTypeId_InValidRange_IsAccepted()
    {
        var validPurchaseTypeIdGen = Gen.Elements(1, 2, 3);

        return Prop.ForAll(
            validPurchaseTypeIdGen.ToArbitrary(),
            (purchaseTypeId) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreatePurchaseMocks();
                var service = CreatePurchaseService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreateValidPurchase(purchaseTypeId);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return result.Success
                    .Label($"PurchaseTypeId={purchaseTypeId} should be accepted but was rejected: {result.Message}");
            });
    }

    // Feature: purchase-classification-enhancements, Property 6: PurchaseTypeId Validation Range
    // **Validates: Requirements 3.3, 3.4, 6.4**
    [Property(MaxTest = 100)]
    public Property PurchaseTypeId_OutsideValidRange_IsRejected()
    {
        // Generate values outside {1, 2, 3}: negatives, zero, and values >= 4
        var invalidPurchaseTypeIdGen = Gen.OneOf(
            Gen.Choose(-1000, 0),
            Gen.Choose(4, 1000)
        );

        return Prop.ForAll(
            invalidPurchaseTypeIdGen.ToArbitrary(),
            (purchaseTypeId) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreatePurchaseMocks();
                var service = CreatePurchaseService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreateValidPurchase(purchaseTypeId);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return (!result.Success)
                    .Label($"PurchaseTypeId={purchaseTypeId} should be rejected but was accepted");
            });
    }

    #endregion
}
