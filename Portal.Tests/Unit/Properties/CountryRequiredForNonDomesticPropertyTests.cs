using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Portal.Tests.Unit.Properties;

// Feature: purchase-classification-enhancements, Property 2: Country Required for Non-Domestic Origin Types

/// <summary>
/// Property-based tests for Country validation on non-domestic origin types.
/// For any PurchaseOriginTypeId in {2, 3, 4} with empty/null Country, validation rejects;
/// with non-empty Country, validation accepts.
/// **Validates: Requirements 1.3, 1.5, 6.3**
/// </summary>
public class CountryRequiredForNonDomesticPropertyTests
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

    private static Purchase CreatePurchaseWithCountry(int originTypeId, string? country)
    {
        return new Purchase
        {
            SupplierId = TestSupplierId,
            ExpenseCategoryId = TestExpenseCategoryId,
            PurchaseOriginTypeId = originTypeId,
            PurchaseTypeId = 3, // Expense (valid default)
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            Description = "Test purchase",
            AmountExcludingVat = 100.00m,
            VatAmount = 15.00m,
            Country = country,
            InvoiceNumber = "INV-001"
        };
    }

    #endregion

    #region Property 2a: Non-domestic origin types with empty/null Country are rejected

    // Feature: purchase-classification-enhancements, Property 2: Country Required for Non-Domestic Origin Types
    // **Validates: Requirements 1.3, 1.5, 6.3**
    [Property(MaxTest = 100)]
    public Property NonDomesticOriginType_WithEmptyOrNullCountry_IsRejected()
    {
        var nonDomesticOriginTypeGen = Gen.Elements(2, 3, 4);
        var emptyCountryGen = Gen.Elements<string?>(null, "", " ", "  ", "\t", "\n", "   \t\n  ", "\r\n");

        return Prop.ForAll(
            nonDomesticOriginTypeGen.ToArbitrary(),
            emptyCountryGen.ToArbitrary(),
            (originTypeId, country) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreatePurchaseMocks();
                var service = CreatePurchaseService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreatePurchaseWithCountry(originTypeId, country);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return (!result.Success)
                    .Label($"OriginTypeId={originTypeId} with Country='{country ?? "null"}' should be rejected but was accepted");
            });
    }

    #endregion

    #region Property 2b: Non-domestic origin types with non-empty Country are accepted

    // Feature: purchase-classification-enhancements, Property 2: Country Required for Non-Domestic Origin Types
    // **Validates: Requirements 1.3, 1.5, 6.3**
    [Property(MaxTest = 100)]
    public Property NonDomesticOriginType_WithNonEmptyCountry_IsAccepted()
    {
        var nonDomesticOriginTypeGen = Gen.Elements(2, 3, 4);
        var nonEmptyCountryGen = Gen.Elements(
            "Ireland", "Germany", "France", "Spain", "Italy",
            "Netherlands", "Belgium", "Portugal", "Austria", "Sweden",
            "United States", "Japan", "China", "Brazil", "Canada");

        return Prop.ForAll(
            nonDomesticOriginTypeGen.ToArbitrary(),
            nonEmptyCountryGen.ToArbitrary(),
            (originTypeId, country) =>
            {
                var (tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock) = CreatePurchaseMocks();
                var service = CreatePurchaseService(tenantMock, supplierRepoMock, expenseCategoryRepoMock, purchaseRepoMock, auditLogRepoMock, dbContextMock);

                var purchase = CreatePurchaseWithCountry(originTypeId, country);

                var result = service.CreatePurchaseAsync(purchase).GetAwaiter().GetResult();

                return result.Success
                    .Label($"OriginTypeId={originTypeId} with Country='{country}' should be accepted but was rejected: {result.Message}");
            });
    }

    #endregion
}
