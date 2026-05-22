using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for PurchaseService bulk entry atomicity.
/// Tests Property 9 from the design document.
/// </summary>
public class BulkEntryPropertyTests
{
    private const int TestBusinessId = 1;
    private const int TestSupplierId = 10;
    private const int TestExpenseCategoryId = 20;

    private static PurchaseService CreateService(
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

    private static (Mock<ICurrentTenantService>, Mock<SupplierRepository>, Mock<ExpenseCategoryRepository>, Mock<PurchaseRepository>, Mock<AuditLogRepository>, Mock<PortalDbContext>) CreateMocks()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContextMock = new Mock<PortalDbContext>(dbContextOptions, tenantMock.Object) { CallBase = true };

        // Mock the Database property to return a mock DatabaseFacade with transaction support
        var transactionMock = new Mock<IDbContextTransaction>();
        transactionMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        transactionMock.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        transactionMock.Setup(t => t.Dispose());

        var databaseFacadeMock = new Mock<DatabaseFacade>(dbContextMock.Object);
        databaseFacadeMock
            .Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionMock.Object);

        dbContextMock.Setup(c => c.Database).Returns(databaseFacadeMock.Object);

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

    private static Purchase CreateValidPurchase()
    {
        return new Purchase
        {
            SupplierId = TestSupplierId,
            ExpenseCategoryId = TestExpenseCategoryId,
            PurchaseOriginTypeId = 1,
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            Description = "Valid purchase",
            AmountExcludingVat = 100.00m,
            VatAmount = 15.00m,
            InvoiceNumber = "INV-001"
        };
    }

    private static Purchase CreateInvalidPurchase(int invalidationType)
    {
        return invalidationType switch
        {
            // AmountExcludingVat <= 0
            0 => new Purchase
            {
                SupplierId = TestSupplierId,
                ExpenseCategoryId = TestExpenseCategoryId,
                PurchaseOriginTypeId = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                Description = "Invalid amount",
                AmountExcludingVat = 0m,
                VatAmount = 10.00m,
                InvoiceNumber = "INV-BAD"
            },
            // Empty Description
            1 => new Purchase
            {
                SupplierId = TestSupplierId,
                ExpenseCategoryId = TestExpenseCategoryId,
                PurchaseOriginTypeId = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                Description = "",
                AmountExcludingVat = 50.00m,
                VatAmount = 7.50m,
                InvoiceNumber = "INV-BAD"
            },
            // Negative VatAmount
            _ => new Purchase
            {
                SupplierId = TestSupplierId,
                ExpenseCategoryId = TestExpenseCategoryId,
                PurchaseOriginTypeId = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                Description = "Negative VAT",
                AmountExcludingVat = 100.00m,
                VatAmount = -5.00m,
                InvoiceNumber = "INV-BAD"
            }
        };
    }

    // Feature: purchase-expense-tracking, Property 9: Batch save atomicity
    // **Validates: Requirements 17.7**
    [Property(MaxTest = 100)]
    public Property Batch_With_Any_Invalid_Row_Persists_Zero_Rows()
    {
        // Generate a batch size between 1 and 10
        var batchSizeGen = Gen.Choose(1, 10);
        // Generate the position of the invalid row (0-indexed)
        var invalidPositionGen = Gen.Choose(0, 9);
        // Generate the type of invalidation (0=zero amount, 1=empty description, 2=negative VAT)
        var invalidTypeGen = Gen.Choose(0, 2);

        return Prop.ForAll(
            batchSizeGen.ToArbitrary(),
            invalidPositionGen.ToArbitrary(),
            invalidTypeGen.ToArbitrary(),
            (batchSize, invalidPosition, invalidType) =>
            {
                // Ensure invalidPosition is within batch bounds
                var actualInvalidPosition = invalidPosition % batchSize;

                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreateMocks();
                var service = CreateService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                // Build batch with one invalid row
                var purchases = new List<Purchase>();
                for (int i = 0; i < batchSize; i++)
                {
                    purchases.Add(i == actualInvalidPosition
                        ? CreateInvalidPurchase(invalidType)
                        : CreateValidPurchase());
                }

                var result = service.BulkCreatePurchasesAsync(purchases).GetAwaiter().GetResult();

                // Verify: result is failure
                var resultFailed = !result.Success;

                // Verify: InsertAsync was never called (zero rows persisted)
                purchaseRepoMock.Verify(
                    r => r.InsertAsync(It.IsAny<Purchase>()),
                    Times.Never());

                return resultFailed
                    .Label($"Batch with invalid row at position {actualInvalidPosition} (type {invalidType}) should fail")
                    .And(true.Label("InsertAsync never called — zero rows persisted"));
            });
    }

    // Feature: purchase-expense-tracking, Property 9: Batch save atomicity
    // **Validates: Requirements 17.7**
    [Property(MaxTest = 100)]
    public Property Batch_With_All_Valid_Rows_Persists_All_Rows()
    {
        // Generate a batch size between 1 and 10
        var batchSizeGen = Gen.Choose(1, 10);

        return Prop.ForAll(
            batchSizeGen.ToArbitrary(),
            (batchSize) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreateMocks();
                var service = CreateService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                // Build batch with all valid rows
                var purchases = new List<Purchase>();
                for (int i = 0; i < batchSize; i++)
                {
                    purchases.Add(CreateValidPurchase());
                }

                var result = service.BulkCreatePurchasesAsync(purchases).GetAwaiter().GetResult();

                // Verify: result is success
                var resultSucceeded = result.Success;

                // Verify: InsertAsync was called exactly batchSize times (all rows persisted)
                purchaseRepoMock.Verify(
                    r => r.InsertAsync(It.IsAny<Purchase>()),
                    Times.Exactly(batchSize));

                return resultSucceeded
                    .Label($"Batch of {batchSize} valid rows should succeed but got: {result.Message}")
                    .And(true.Label($"InsertAsync called {batchSize} times — all rows persisted"));
            });
    }
}
