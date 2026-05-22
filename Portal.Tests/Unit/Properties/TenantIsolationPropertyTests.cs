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
/// Property-based tests for tenant isolation (BusinessId assignment).
/// Tests Property 7 from the design document.
/// **Validates: Requirements 4.4, 5.4, 6.4, 8.4**
/// </summary>
public class TenantIsolationPropertyTests
{
    private const int TenantBusinessId = 42;
    private const int TestSupplierId = 10;
    private const int TestExpenseCategoryId = 20;

    #region Supplier Service Helpers

    private static (SupplierService service, Mock<SupplierRepository> supplierRepoMock) CreateSupplierService(int tenantBusinessId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(tenantBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContextMock = new Mock<PortalDbContext>(dbContextOptions, tenantMock.Object) { CallBase = false };

        var supplierRepoMock = new Mock<SupplierRepository>(dbContextMock.Object) { CallBase = false };
        supplierRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<Supplier>()))
            .ReturnsAsync(1);

        var auditLogRepoMock = new Mock<AuditLogRepository>(dbContextMock.Object) { CallBase = false };
        auditLogRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        var service = new SupplierService(supplierRepoMock.Object, auditLogRepoMock.Object, tenantMock.Object);
        return (service, supplierRepoMock);
    }

    #endregion

    #region ExpenseCategory Service Helpers

    private static (ExpenseCategoryService service, Mock<ExpenseCategoryRepository> expenseCategoryRepoMock) CreateExpenseCategoryService(int tenantBusinessId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(tenantBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContextMock = new Mock<PortalDbContext>(dbContextOptions, tenantMock.Object) { CallBase = false };

        var expenseCategoryRepoMock = new Mock<ExpenseCategoryRepository>(dbContextMock.Object) { CallBase = false };
        expenseCategoryRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<ExpenseCategory>()))
            .ReturnsAsync(1);

        var auditLogRepoMock = new Mock<AuditLogRepository>(dbContextMock.Object) { CallBase = false };
        auditLogRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        var service = new ExpenseCategoryService(expenseCategoryRepoMock.Object, auditLogRepoMock.Object, tenantMock.Object);
        return (service, expenseCategoryRepoMock);
    }

    #endregion

    #region Purchase Service Helpers

    private static (PurchaseService service, Mock<PurchaseRepository> purchaseRepoMock) CreatePurchaseService(int tenantBusinessId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(tenantBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContextMock = new Mock<PortalDbContext>(dbContextOptions, tenantMock.Object) { CallBase = true };

        var supplierRepoMock = new Mock<SupplierRepository>(dbContextMock.Object) { CallBase = false };
        supplierRepoMock
            .Setup(r => r.GetByIdAndBusinessIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new Supplier { Id = TestSupplierId, BusinessId = tenantBusinessId, Name = "Test Supplier", IsActive = true });

        var expenseCategoryRepoMock = new Mock<ExpenseCategoryRepository>(dbContextMock.Object) { CallBase = false };
        expenseCategoryRepoMock
            .Setup(r => r.GetByIdAndBusinessIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new ExpenseCategory { Id = TestExpenseCategoryId, BusinessId = tenantBusinessId, Name = "Test Category", IsActive = true });

        var purchaseRepoMock = new Mock<PurchaseRepository>(dbContextMock.Object) { CallBase = false };
        purchaseRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<Purchase>()))
            .Returns(Task.CompletedTask);

        var auditLogRepoMock = new Mock<AuditLogRepository>(dbContextMock.Object) { CallBase = false };
        auditLogRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        var service = new PurchaseService(
            tenantMock.Object,
            purchaseRepoMock.Object,
            supplierRepoMock.Object,
            expenseCategoryRepoMock.Object,
            auditLogRepoMock.Object,
            dbContextMock.Object);

        return (service, purchaseRepoMock);
    }

    #endregion

    // Feature: purchase-expense-tracking, Property 7: Tenant BusinessId assignment
    // **Validates: Requirements 4.4, 5.4, 6.4, 8.4**
    [Property(MaxTest = 100)]
    public Property Supplier_BusinessId_Is_Overwritten_By_CurrentTenantService()
    {
        var randomBusinessIdGen = Gen.Choose(1, 999999);

        return Prop.ForAll(
            randomBusinessIdGen.ToArbitrary(),
            (inputBusinessId) =>
            {
                var (service, supplierRepoMock) = CreateSupplierService(TenantBusinessId);

                var supplier = new Supplier
                {
                    BusinessId = inputBusinessId,
                    Name = "Test Supplier"
                };

                var result = service.CreateSupplierAsync(supplier).GetAwaiter().GetResult();

                return result.Success.Label("Service call succeeded")
                    .And((supplier.BusinessId == TenantBusinessId)
                        .Label($"BusinessId should be {TenantBusinessId} (tenant) but was {supplier.BusinessId} (input was {inputBusinessId})"));
            });
    }

    // Feature: purchase-expense-tracking, Property 7: Tenant BusinessId assignment
    // **Validates: Requirements 4.4, 5.4, 6.4, 8.4**
    [Property(MaxTest = 100)]
    public Property ExpenseCategory_BusinessId_Is_Overwritten_By_CurrentTenantService()
    {
        var randomBusinessIdGen = Gen.Choose(1, 999999);

        return Prop.ForAll(
            randomBusinessIdGen.ToArbitrary(),
            (inputBusinessId) =>
            {
                var (service, expenseCategoryRepoMock) = CreateExpenseCategoryService(TenantBusinessId);

                var category = new ExpenseCategory
                {
                    BusinessId = inputBusinessId,
                    Name = "Test Category"
                };

                var result = service.CreateExpenseCategoryAsync(category).GetAwaiter().GetResult();

                return result.Success.Label("Service call succeeded")
                    .And((category.BusinessId == TenantBusinessId)
                        .Label($"BusinessId should be {TenantBusinessId} (tenant) but was {category.BusinessId} (input was {inputBusinessId})"));
            });
    }

    // Feature: purchase-expense-tracking, Property 7: Tenant BusinessId assignment
    // **Validates: Requirements 4.4, 5.4, 6.4, 8.4**
    [Property(MaxTest = 100)]
    public Property Purchase_BusinessId_Is_Overwritten_By_CurrentTenantService()
    {
        var randomBusinessIdGen = Gen.Choose(1, 999999);
        var amountGen = Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var vatGen = Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));

        return Prop.ForAll(
            randomBusinessIdGen.ToArbitrary(),
            amountGen.ToArbitrary(),
            vatGen.ToArbitrary(),
            (inputBusinessId, amountExclVat, vatAmount) =>
            {
                var (service, purchaseRepoMock) = CreatePurchaseService(TenantBusinessId);

                var purchase = new Purchase
                {
                    BusinessId = inputBusinessId,
                    SupplierId = TestSupplierId,
                    ExpenseCategoryId = TestExpenseCategoryId,
                    PurchaseOriginTypeId = 1,
                    InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                    Description = "Test purchase",
                    AmountExcludingVat = amountExclVat,
                    VatAmount = vatAmount,
                    InvoiceNumber = "INV-001"
                };

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return result.Success.Label("Service call succeeded")
                    .And((purchase.BusinessId == TenantBusinessId)
                        .Label($"BusinessId should be {TenantBusinessId} (tenant) but was {purchase.BusinessId} (input was {inputBusinessId})"));
            });
    }
}
