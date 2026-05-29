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
/// Property-based tests for TotalAmount computation by origin type.
/// Feature: purchase-classification-enhancements, Property 3: TotalAmount Computation by Origin Type
/// 
/// EU RC → VatAmount=0, TotalAmount=AmountExcludingVat;
/// Domestic/Non-EU/EU Paid → TotalAmount=AmountExcludingVat+VatAmount
/// 
/// **Validates: Requirements 6.2**
/// </summary>
public class TotalAmountComputationPropertyTests
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

    private static Purchase CreateValidPurchase(int originTypeId, decimal amountExclVat, decimal vatAmount)
    {
        // Non-domestic origin types require a Country value
        string? country = originTypeId != 1 ? "Germany" : null;

        return new Purchase
        {
            SupplierId = TestSupplierId,
            ExpenseCategoryId = TestExpenseCategoryId,
            PurchaseOriginTypeId = originTypeId,
            PurchaseTypeId = 3,
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            Description = "Test purchase",
            AmountExcludingVat = amountExclVat,
            VatAmount = vatAmount,
            Country = country,
            InvoiceNumber = "INV-001"
        };
    }

    // Feature: purchase-classification-enhancements, Property 3: TotalAmount Computation by Origin Type
    // EU RC (Id=2) → VatAmount=0, TotalAmount=AmountExcludingVat
    // **Validates: Requirements 6.2**
    [Property(MaxTest = 100)]
    public Property EuReverseCharge_Sets_VatAmount_To_Zero_And_TotalAmount_Equals_AmountExcludingVat()
    {
        var amountGen = Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var vatGen = Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));

        return Prop.ForAll(
            amountGen.ToArbitrary(),
            vatGen.ToArbitrary(),
            (amountExclVat, vatAmount) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreateMocks();
                var service = CreateService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreateValidPurchase(2, amountExclVat, vatAmount);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return result.Success.Label("Service call succeeded")
                    .And((purchase.VatAmount == 0m)
                        .Label($"EU RC: VatAmount should be 0 but was {purchase.VatAmount}"))
                    .And((purchase.TotalAmount == amountExclVat)
                        .Label($"EU RC: TotalAmount ({purchase.TotalAmount}) should equal AmountExcludingVat ({amountExclVat})"));
            });
    }

    // Feature: purchase-classification-enhancements, Property 3: TotalAmount Computation by Origin Type
    // Domestic (Id=1), Non-EU (Id=3), EU Paid (Id=4) → TotalAmount=AmountExcludingVat+VatAmount
    // **Validates: Requirements 6.2**
    [Property(MaxTest = 100)]
    public Property Domestic_NonEu_EuPaid_TotalAmount_Equals_AmountExcludingVat_Plus_VatAmount()
    {
        var amountGen = Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var vatGen = Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var originTypeGen = Gen.Elements(1, 3, 4); // Domestic, Non-EU, EU Paid

        return Prop.ForAll(
            amountGen.ToArbitrary(),
            vatGen.ToArbitrary(),
            originTypeGen.ToArbitrary(),
            (amountExclVat, vatAmount, originTypeId) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreateMocks();
                var service = CreateService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreateValidPurchase(originTypeId, amountExclVat, vatAmount);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                var expectedTotal = amountExclVat + vatAmount;

                return result.Success.Label($"Service call succeeded for originTypeId={originTypeId}")
                    .And((purchase.TotalAmount == expectedTotal)
                        .Label($"OriginType {originTypeId}: TotalAmount ({purchase.TotalAmount}) should equal AmountExcludingVat ({amountExclVat}) + VatAmount ({vatAmount}) = {expectedTotal}"));
            });
    }
}
