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
/// Property-based tests for PurchaseService VAT logic.
/// Tests Properties 1, 2, 4, and 12 from the design document.
/// </summary>
public class PurchaseVatPropertyTests
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

    private static Purchase CreateValidPurchase(int originTypeId, decimal amountExclVat, decimal vatAmount, string? country = null)
    {
        return new Purchase
        {
            SupplierId = TestSupplierId,
            ExpenseCategoryId = TestExpenseCategoryId,
            PurchaseOriginTypeId = originTypeId,
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            Description = "Test purchase",
            AmountExcludingVat = amountExclVat,
            VatAmount = vatAmount,
            Country = country,
            InvoiceNumber = "INV-001"
        };
    }

    // Feature: purchase-expense-tracking, Property 1: TotalAmount equals AmountExcludingVat plus VatAmount
    // **Validates: Requirements 6.6, 7.5, 7.6**
    [Property(MaxTest = 100)]
    public Property TotalAmount_Equals_AmountExcludingVat_Plus_VatAmount_For_Domestic_And_NonEu()
    {
        var amountGen = Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var vatGen = Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var originTypeGen = Gen.Elements(1, 3);

        return Prop.ForAll(
            amountGen.ToArbitrary(),
            vatGen.ToArbitrary(),
            originTypeGen.ToArbitrary(),
            (amountExclVat, vatAmount, originTypeId) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreateMocks();
                var service = CreateService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var country = originTypeId == 3 ? "Germany" : null;
                var purchase = CreateValidPurchase(originTypeId, amountExclVat, vatAmount, country);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return result.Success.Label("Service call succeeded")
                    .And((purchase.TotalAmount == purchase.AmountExcludingVat + purchase.VatAmount)
                        .Label($"TotalAmount ({purchase.TotalAmount}) == AmountExcludingVat ({purchase.AmountExcludingVat}) + VatAmount ({purchase.VatAmount})"));
            });
    }

    // Feature: purchase-expense-tracking, Property 2: EU Reverse Charge forces VatAmount to zero
    // **Validates: Requirements 7.2, 7.3**
    [Property(MaxTest = 100)]
    public Property EuReverseCharge_Forces_VatAmount_To_Zero()
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

                var purchase = CreateValidPurchase(2, amountExclVat, vatAmount, "France");

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return result.Success.Label("Service call succeeded")
                    .And((purchase.VatAmount == 0m)
                        .Label($"VatAmount should be 0 but was {purchase.VatAmount}"))
                    .And((purchase.TotalAmount == amountExclVat)
                        .Label($"TotalAmount ({purchase.TotalAmount}) should equal AmountExcludingVat ({amountExclVat})"));
            });
    }

    // Feature: purchase-expense-tracking, Property 4: Domestic/Non-EU preserves user-provided VatAmount
    // **Validates: Requirements 7.5, 7.6**
    [Property(MaxTest = 100)]
    public Property Domestic_And_NonEu_Preserves_User_Provided_VatAmount()
    {
        var amountGen = Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var vatGen = Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var originTypeGen = Gen.Elements(1, 3);

        return Prop.ForAll(
            amountGen.ToArbitrary(),
            vatGen.ToArbitrary(),
            originTypeGen.ToArbitrary(),
            (amountExclVat, vatAmount, originTypeId) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreateMocks();
                var service = CreateService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var country = originTypeId == 3 ? "Germany" : null;
                var purchase = CreateValidPurchase(originTypeId, amountExclVat, vatAmount, country);

                var originalVatAmount = vatAmount;
                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return result.Success.Label("Service call succeeded")
                    .And((purchase.VatAmount == originalVatAmount)
                        .Label($"VatAmount should be preserved as {originalVatAmount} but was {purchase.VatAmount}"));
            });
    }

    // Feature: purchase-expense-tracking, Property 12: Domestic allows null Country
    // **Validates: Requirements 7.5**
    [Property(MaxTest = 100)]
    public Property Domestic_Allows_Null_Or_Whitespace_Country()
    {
        var amountGen = Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var vatGen = Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var nullOrWhitespaceGen = Gen.Elements<string?>(null, "", " ", "  ", "\t", "\n", "   \t\n  ");

        return Prop.ForAll(
            amountGen.ToArbitrary(),
            vatGen.ToArbitrary(),
            nullOrWhitespaceGen.ToArbitrary(),
            (amountExclVat, vatAmount, country) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreateMocks();
                var service = CreateService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreateValidPurchase(1, amountExclVat, vatAmount, country);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return result.Success
                    .Label($"Domestic purchase with Country='{country ?? "null"}' should succeed but failed: {result.Message}");
            });
    }
}
