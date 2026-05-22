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
/// Property-based tests for validation rules.
/// Tests Properties 3, 5, and 6 from the design document.
/// </summary>
public class ValidationPropertyTests
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

    private static (Mock<ICurrentTenantService>, Mock<SupplierRepository>, Mock<AuditLogRepository>, Mock<PortalDbContext>) CreateSupplierMocks()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

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

        return (tenantMock, supplierRepoMock, auditLogRepoMock, dbContextMock);
    }

    private static (Mock<ICurrentTenantService>, Mock<ExpenseCategoryRepository>, Mock<AuditLogRepository>, Mock<PortalDbContext>) CreateExpenseCategoryMocks()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

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

        return (tenantMock, expenseCategoryRepoMock, auditLogRepoMock, dbContextMock);
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

    #endregion

    #region Property 3: EU RC/Non-EU requires non-whitespace Country

    // Feature: purchase-expense-tracking, Property 3: EU Reverse Charge and Non-EU require non-whitespace Country
    // **Validates: Requirements 7.4**
    [Property(MaxTest = 100)]
    public Property EuReverseCharge_Rejects_NullOrWhitespace_Country()
    {
        var amountGen = Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var vatGen = Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var whitespaceGen = Gen.Elements<string?>(null, "", " ", "  ", "\t", "\n", "   \t\n  ", "\r\n");

        return Prop.ForAll(
            amountGen.ToArbitrary(),
            vatGen.ToArbitrary(),
            whitespaceGen.ToArbitrary(),
            (amountExclVat, vatAmount, country) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreatePurchaseMocks();
                var service = CreatePurchaseService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreateValidPurchase(2, amountExclVat, vatAmount, country);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return (!result.Success)
                    .Label($"EU RC with Country='{country ?? "null"}' should be rejected but was accepted");
            });
    }

    // Feature: purchase-expense-tracking, Property 3: EU Reverse Charge and Non-EU require non-whitespace Country
    // **Validates: Requirements 7.4**
    [Property(MaxTest = 100)]
    public Property NonEu_Rejects_NullOrWhitespace_Country()
    {
        var amountGen = Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var vatGen = Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var whitespaceGen = Gen.Elements<string?>(null, "", " ", "  ", "\t", "\n", "   \t\n  ", "\r\n");

        return Prop.ForAll(
            amountGen.ToArbitrary(),
            vatGen.ToArbitrary(),
            whitespaceGen.ToArbitrary(),
            (amountExclVat, vatAmount, country) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreatePurchaseMocks();
                var service = CreatePurchaseService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreateValidPurchase(3, amountExclVat, vatAmount, country);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return (!result.Success)
                    .Label($"Non-EU with Country='{country ?? "null"}' should be rejected but was accepted");
            });
    }

    #endregion

    #region Property 5: Whitespace rejection for required text fields

    // Feature: purchase-expense-tracking, Property 5: Whitespace rejection for required text fields
    // **Validates: Requirements 4.7**
    [Property(MaxTest = 100)]
    public Property SupplierService_Rejects_NullOrWhitespace_Name()
    {
        var whitespaceGen = Gen.Elements<string?>(null, "", " ", "  ", "\t", "\n", "   \t\n  ", "\r\n");

        return Prop.ForAll(
            whitespaceGen.ToArbitrary(),
            (name) =>
            {
                var (tenantMock, supplierRepoMock, auditLogRepoMock, _) = CreateSupplierMocks();
                var service = new SupplierService(
                    supplierRepoMock.Object,
                    auditLogRepoMock.Object,
                    tenantMock.Object);

                var supplier = new Supplier { Name = name! };

                var result = service.CreateSupplierAsync(supplier).GetAwaiter().GetResult();

                return (!result.Success)
                    .Label($"Supplier with Name='{name ?? "null"}' should be rejected but was accepted");
            });
    }

    // Feature: purchase-expense-tracking, Property 5: Whitespace rejection for required text fields
    // **Validates: Requirements 5.7**
    [Property(MaxTest = 100)]
    public Property ExpenseCategoryService_Rejects_NullOrWhitespace_Name()
    {
        var whitespaceGen = Gen.Elements<string?>(null, "", " ", "  ", "\t", "\n", "   \t\n  ", "\r\n");

        return Prop.ForAll(
            whitespaceGen.ToArbitrary(),
            (name) =>
            {
                var (tenantMock, expenseCategoryRepoMock, auditLogRepoMock, _) = CreateExpenseCategoryMocks();
                var service = new ExpenseCategoryService(
                    expenseCategoryRepoMock.Object,
                    auditLogRepoMock.Object,
                    tenantMock.Object);

                var category = new ExpenseCategory { Name = name! };

                var result = service.CreateExpenseCategoryAsync(category).GetAwaiter().GetResult();

                return (!result.Success)
                    .Label($"ExpenseCategory with Name='{name ?? "null"}' should be rejected but was accepted");
            });
    }

    // Feature: purchase-expense-tracking, Property 5: Whitespace rejection for required text fields
    // **Validates: Requirements 6.11**
    [Property(MaxTest = 100)]
    public Property PurchaseService_Rejects_NullOrWhitespace_Description()
    {
        var amountGen = Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var vatGen = Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var whitespaceGen = Gen.Elements<string?>(null, "", " ", "  ", "\t", "\n", "   \t\n  ", "\r\n");

        return Prop.ForAll(
            amountGen.ToArbitrary(),
            vatGen.ToArbitrary(),
            whitespaceGen.ToArbitrary(),
            (amountExclVat, vatAmount, description) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreatePurchaseMocks();
                var service = CreatePurchaseService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = new Purchase
                {
                    SupplierId = TestSupplierId,
                    ExpenseCategoryId = TestExpenseCategoryId,
                    PurchaseOriginTypeId = 1,
                    InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                    Description = description!,
                    AmountExcludingVat = amountExclVat,
                    VatAmount = vatAmount,
                    InvoiceNumber = "INV-001"
                };

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return (!result.Success)
                    .Label($"Purchase with Description='{description ?? "null"}' should be rejected but was accepted");
            });
    }

    #endregion

    #region Property 6: Numeric validation bounds

    // Feature: purchase-expense-tracking, Property 6: Numeric validation bounds
    // **Validates: Requirements 6.7**
    [Property(MaxTest = 100)]
    public Property PurchaseService_Rejects_NonPositive_AmountExcludingVat()
    {
        var nonPositiveAmountGen = Gen.Choose(-99999999, 0).Select(i => Math.Round((decimal)i / 100m, 2));
        var vatGen = Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));

        return Prop.ForAll(
            nonPositiveAmountGen.ToArbitrary(),
            vatGen.ToArbitrary(),
            (amountExclVat, vatAmount) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreatePurchaseMocks();
                var service = CreatePurchaseService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreateValidPurchase(1, amountExclVat, vatAmount);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return (!result.Success)
                    .Label($"Purchase with AmountExcludingVat={amountExclVat} should be rejected but was accepted");
            });
    }

    // Feature: purchase-expense-tracking, Property 6: Numeric validation bounds
    // **Validates: Requirements 6.8**
    [Property(MaxTest = 100)]
    public Property PurchaseService_Rejects_Negative_VatAmount()
    {
        var amountGen = Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
        var negativeVatGen = Gen.Choose(-99999999, -1).Select(i => Math.Round((decimal)i / 100m, 2));

        return Prop.ForAll(
            amountGen.ToArbitrary(),
            negativeVatGen.ToArbitrary(),
            (amountExclVat, vatAmount) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreatePurchaseMocks();
                var service = CreatePurchaseService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreateValidPurchase(1, amountExclVat, vatAmount);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return (!result.Success)
                    .Label($"Purchase with VatAmount={vatAmount} should be rejected but was accepted");
            });
    }

    #endregion
}
