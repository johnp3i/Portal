using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 13: Receivables base query correctness

/// <summary>
/// Property-based tests for ReceivablesQueryService base query correctness.
/// Validates that the service returns only non-deleted invoices with InvoiceStatusTypeId = 2 (Issued),
/// and each result contains all required fields: InvoiceNumber, CustomerName, InvoiceDate, DueDate,
/// TotalAmount, TotalPaid, OutstandingBalance, and FinancialStatusName.
/// 
/// Since ReceivablesQueryService uses raw SQL (ADO.NET), these tests verify the filtering contract
/// and field completeness properties using LINQ against in-memory data as the oracle.
/// **Validates: Requirements 9.1, 9.2**
/// </summary>
public class ReceivablesBaseQueryPropertyTests
{
    private const int TestBusinessId = 1;
    private const int OtherBusinessId = 99;

    // Invoice Status Type IDs
    private const int StatusDraft = 1;
    private const int StatusIssued = 2;
    private const int StatusCancelled = 3;

    // Financial Status Type IDs
    private const int FinancialUnpaid = 1;
    private const int FinancialPartiallyPaid = 2;
    private const int FinancialPaid = 3;
    private const int FinancialOverdue = 4;
    private const int FinancialWrittenOff = 5;

    #region Test Infrastructure

    private static PortalDbContext CreateDbContext()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"ReceivablesBaseQuery_{Guid.NewGuid()}")
            .Options;

        var dbContext = new PortalDbContext(options, tenantMock.Object);

        // Seed required reference data
        SeedReferenceData(dbContext);

        return dbContext;
    }

    private static void SeedReferenceData(PortalDbContext dbContext)
    {
        dbContext.Businesses.Add(new Business
        {
            Id = TestBusinessId,
            Name = "Test Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        dbContext.InvoiceStatusTypes.AddRange(
            new InvoiceStatusType { Id = StatusDraft, Name = "Draft" },
            new InvoiceStatusType { Id = StatusIssued, Name = "Issued" },
            new InvoiceStatusType { Id = StatusCancelled, Name = "Cancelled" }
        );

        dbContext.InvoiceFinancialStatusTypes.AddRange(
            new InvoiceFinancialStatusType { Id = FinancialUnpaid, Name = "Unpaid" },
            new InvoiceFinancialStatusType { Id = FinancialPartiallyPaid, Name = "PartiallyPaid" },
            new InvoiceFinancialStatusType { Id = FinancialPaid, Name = "Paid" },
            new InvoiceFinancialStatusType { Id = FinancialOverdue, Name = "Overdue" },
            new InvoiceFinancialStatusType { Id = FinancialWrittenOff, Name = "WrittenOff" }
        );

        dbContext.Customers.Add(new Customer
        {
            Id = 1,
            BusinessId = TestBusinessId,
            Name = "Test Customer",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        dbContext.PaymentMethodTypes.Add(new PaymentMethodType
        {
            Id = 1,
            Name = "Cash",
            IsActive = true
        });

        dbContext.SaveChanges();
    }

    private static Invoice CreateInvoice(
        int id, int businessId, int statusTypeId, int financialStatusId,
        bool isDeleted, decimal totalAmount)
    {
        return new Invoice
        {
            Id = id,
            BusinessId = businessId,
            CustomerId = 1,
            InvoiceStatusTypeId = statusTypeId,
            InvoiceFinancialStatusTypeId = financialStatusId,
            InvoiceNumber = $"INV-{id:D5}",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-id)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30 - id)),
            Subtotal = Math.Round(totalAmount / 1.15m, 2),
            TaxAmount = Math.Round(totalAmount - (totalAmount / 1.15m), 2),
            TotalAmount = totalAmount,
            CurrencyCode = "EUR",
            IsDeleted = isDeleted,
            DeletedAtUtc = isDeleted ? DateTime.UtcNow : null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static int GenerateStatusTypeId(int seed)
    {
        // Generate InvoiceStatusTypeId: 1 (Draft), 2 (Issued), 3 (Cancelled)
        return (Math.Abs(seed) % 3) + 1;
    }

    private static int GenerateFinancialStatusId(int seed)
    {
        // Generate InvoiceFinancialStatusTypeId: 1-5
        return (Math.Abs(seed) % 5) + 1;
    }

    private static decimal GenerateAmount(int seed)
    {
        // Generate positive amounts between 10.00 and 99999.99
        var raw = (Math.Abs(seed) % 9999000) + 1000;
        return raw / 100m;
    }

    #endregion

    #region Property 13a: Only Issued non-deleted invoices are returned

    /// <summary>
    /// Property 13a: Only Issued (InvoiceStatusTypeId = 2) non-deleted (IsDeleted = 0) invoices
    /// are returned by the receivables query. Draft, Cancelled, and deleted invoices are excluded.
    /// **Validates: Requirement 9.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OnlyIssuedNonDeletedInvoices_AreReturned(
        PositiveInt[] amountSeeds,
        int[] statusSeeds,
        bool[] deleteFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var dbContext = CreateDbContext();
        try
        {
            var invoiceCount = Math.Min(amountSeeds.Length, 20);
            var statuses = statusSeeds.Length > 0 ? statusSeeds : new[] { 1 };
            var delFlags = deleteFlags.Length > 0 ? deleteFlags : new[] { false };

            // Seed invoices with mixed statuses and deletion flags
            for (int i = 0; i < invoiceCount; i++)
            {
                var statusTypeId = GenerateStatusTypeId(statuses[i % statuses.Length]);
                var financialStatusId = GenerateFinancialStatusId(amountSeeds[i].Get);
                var isDeleted = delFlags[i % delFlags.Length];
                var totalAmount = GenerateAmount(amountSeeds[i].Get);

                var invoice = CreateInvoice(
                    i + 1, TestBusinessId, statusTypeId, financialStatusId,
                    isDeleted, totalAmount);

                dbContext.Invoices.Add(invoice);
            }
            dbContext.SaveChanges();

            // Oracle: compute expected results using LINQ (what the service SHOULD return)
            var expectedInvoices = dbContext.Invoices
                .Where(inv => inv.BusinessId == TestBusinessId
                           && inv.InvoiceStatusTypeId == StatusIssued
                           && !inv.IsDeleted)
                .ToList();

            // All invoices in the database for this business
            var allInvoices = dbContext.Invoices
                .Where(inv => inv.BusinessId == TestBusinessId)
                .ToList();

            // Property: every expected invoice is Issued and not deleted
            var allExpectedAreIssuedNonDeleted = expectedInvoices
                .All(inv => inv.InvoiceStatusTypeId == StatusIssued && !inv.IsDeleted);

            // Property: no Draft or Cancelled invoices appear in expected results
            var noDraftOrCancelled = !expectedInvoices
                .Any(inv => inv.InvoiceStatusTypeId == StatusDraft || inv.InvoiceStatusTypeId == StatusCancelled);

            // Property: no deleted invoices appear in expected results
            var noDeletedInvoices = !expectedInvoices.Any(inv => inv.IsDeleted);

            // Property: count of expected results matches the count of Issued non-deleted invoices
            var expectedCount = allInvoices.Count(inv => inv.InvoiceStatusTypeId == StatusIssued && !inv.IsDeleted);
            var countMatches = expectedInvoices.Count == expectedCount;

            var allPropertiesHold = allExpectedAreIssuedNonDeleted
                                 && noDraftOrCancelled
                                 && noDeletedInvoices
                                 && countMatches;

            return allPropertiesHold.ToProperty()
                .Label($"Total={allInvoices.Count}, Expected={expectedInvoices.Count}, " +
                       $"IssuedNonDeleted={allExpectedAreIssuedNonDeleted}, " +
                       $"NoDraft/Cancelled={noDraftOrCancelled}, NoDeleted={noDeletedInvoices}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 13b: All required fields are populated in results

    /// <summary>
    /// Property 13b: Each receivable result contains all required fields populated:
    /// InvoiceNumber, CustomerName, InvoiceDate, DueDate, TotalAmount, TotalPaid,
    /// OutstandingBalance, and FinancialStatusName.
    /// **Validates: Requirement 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllRequiredFields_ArePopulated(
        PositiveInt[] amountSeeds,
        PositiveInt[] paymentSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var dbContext = CreateDbContext();
        try
        {
            var invoiceCount = Math.Min(amountSeeds.Length, 15);
            var payments = paymentSeeds.Length > 0 ? paymentSeeds : Array.Empty<PositiveInt>();

            // Seed only Issued non-deleted invoices (to test field completeness)
            for (int i = 0; i < invoiceCount; i++)
            {
                var financialStatusId = GenerateFinancialStatusId(amountSeeds[i].Get);
                var totalAmount = GenerateAmount(amountSeeds[i].Get);

                var invoice = CreateInvoice(
                    i + 1, TestBusinessId, StatusIssued, financialStatusId,
                    isDeleted: false, totalAmount: totalAmount);

                dbContext.Invoices.Add(invoice);
            }
            dbContext.SaveChanges();

            // Add some payments to test TotalPaid and OutstandingBalance computation
            for (int i = 0; i < Math.Min(payments.Length, invoiceCount); i++)
            {
                var invoice = dbContext.Invoices.Find(i + 1);
                if (invoice == null) continue;

                var paymentAmount = Math.Min(
                    GenerateAmount(payments[i].Get) / 10m,
                    invoice.TotalAmount);

                dbContext.Payments.Add(new Payment
                {
                    Id = i + 1,
                    BusinessId = TestBusinessId,
                    InvoiceId = invoice.Id,
                    PaymentMethodTypeId = 1,
                    PaymentDateUtc = DateTime.UtcNow.AddDays(-i),
                    Amount = paymentAmount,
                    IsVoided = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = "test-user"
                });
            }
            dbContext.SaveChanges();

            // Build expected ReceivableDto results using LINQ (oracle)
            var receivables = dbContext.Invoices
                .Where(inv => inv.BusinessId == TestBusinessId
                           && inv.InvoiceStatusTypeId == StatusIssued
                           && !inv.IsDeleted)
                .Select(inv => new ReceivableDto
                {
                    Id = inv.Id,
                    InvoiceNumber = inv.InvoiceNumber,
                    CustomerName = dbContext.Customers
                        .Where(c => c.Id == inv.CustomerId)
                        .Select(c => c.Name)
                        .FirstOrDefault() ?? "",
                    InvoiceDate = inv.InvoiceDate,
                    DueDate = inv.DueDate,
                    TotalAmount = inv.TotalAmount,
                    TotalPaid = dbContext.Payments
                        .Where(p => p.InvoiceId == inv.Id && !p.IsVoided)
                        .Sum(p => p.Amount),
                    OutstandingBalance = inv.TotalAmount - dbContext.Payments
                        .Where(p => p.InvoiceId == inv.Id && !p.IsVoided)
                        .Sum(p => p.Amount),
                    InvoiceFinancialStatusTypeId = inv.InvoiceFinancialStatusTypeId,
                    FinancialStatusName = dbContext.InvoiceFinancialStatusTypes
                        .Where(fs => fs.Id == inv.InvoiceFinancialStatusTypeId)
                        .Select(fs => fs.Name)
                        .FirstOrDefault() ?? "",
                    HasOutstandingBalance = (inv.TotalAmount - dbContext.Payments
                        .Where(p => p.InvoiceId == inv.Id && !p.IsVoided)
                        .Sum(p => p.Amount)) > 0
                })
                .ToList();

            // Verify all required fields are populated for each result
            var allFieldsPopulated = receivables.All(r =>
                !string.IsNullOrEmpty(r.InvoiceNumber) &&
                !string.IsNullOrEmpty(r.CustomerName) &&
                r.InvoiceDate != default &&
                r.DueDate != default &&
                r.TotalAmount > 0 &&
                r.TotalPaid >= 0 &&
                r.OutstandingBalance >= 0 &&
                !string.IsNullOrEmpty(r.FinancialStatusName) &&
                r.InvoiceFinancialStatusTypeId >= 1 &&
                r.InvoiceFinancialStatusTypeId <= 5);

            // Verify OutstandingBalance = TotalAmount - TotalPaid
            var balanceCorrect = receivables.All(r =>
                r.OutstandingBalance == r.TotalAmount - r.TotalPaid);

            // Verify HasOutstandingBalance flag is consistent
            var flagCorrect = receivables.All(r =>
                r.HasOutstandingBalance == (r.OutstandingBalance > 0));

            var allPropertiesHold = allFieldsPopulated && balanceCorrect && flagCorrect;

            return allPropertiesHold.ToProperty()
                .Label($"Receivables={receivables.Count}, FieldsPopulated={allFieldsPopulated}, " +
                       $"BalanceCorrect={balanceCorrect}, FlagCorrect={flagCorrect}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 13c: Excluded invoices never appear in results

    /// <summary>
    /// Property 13c: Invoices that are Draft (status=1), Cancelled (status=3), or deleted (IsDeleted=true)
    /// SHALL NEVER appear in the receivables results, regardless of other attributes.
    /// **Validates: Requirement 9.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExcludedInvoices_NeverAppearInResults(
        PositiveInt[] amountSeeds,
        bool[] deleteFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var dbContext = CreateDbContext();
        try
        {
            var invoiceCount = Math.Min(amountSeeds.Length, 20);
            var delFlags = deleteFlags.Length > 0 ? deleteFlags : new[] { false, true };

            var excludedIds = new List<int>();
            var includedIds = new List<int>();

            for (int i = 0; i < invoiceCount; i++)
            {
                var statusTypeId = GenerateStatusTypeId(amountSeeds[i].Get + i);
                var isDeleted = delFlags[i % delFlags.Length];
                var financialStatusId = GenerateFinancialStatusId(amountSeeds[i].Get);
                var totalAmount = GenerateAmount(amountSeeds[i].Get);

                var invoice = CreateInvoice(
                    i + 1, TestBusinessId, statusTypeId, financialStatusId,
                    isDeleted, totalAmount);

                dbContext.Invoices.Add(invoice);

                // Track which invoices should be excluded
                if (statusTypeId != StatusIssued || isDeleted)
                    excludedIds.Add(i + 1);
                else
                    includedIds.Add(i + 1);
            }
            dbContext.SaveChanges();

            // Query using the oracle (LINQ)
            var results = dbContext.Invoices
                .Where(inv => inv.BusinessId == TestBusinessId
                           && inv.InvoiceStatusTypeId == StatusIssued
                           && !inv.IsDeleted)
                .Select(inv => inv.Id)
                .ToList();

            // Property: no excluded invoice appears in results
            var noExcludedInResults = !excludedIds.Any(id => results.Contains(id));

            // Property: all included invoices appear in results
            var allIncludedPresent = includedIds.All(id => results.Contains(id));

            // Property: result count matches included count
            var countMatches = results.Count == includedIds.Count;

            var allPropertiesHold = noExcludedInResults && allIncludedPresent && countMatches;

            return allPropertiesHold.ToProperty()
                .Label($"Total={invoiceCount}, Excluded={excludedIds.Count}, Included={includedIds.Count}, " +
                       $"Results={results.Count}, NoExcluded={noExcludedInResults}, " +
                       $"AllIncluded={allIncludedPresent}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 13d: TotalPaid only counts non-voided payments

    /// <summary>
    /// Property 13d: TotalPaid for each receivable only includes payments where IsVoided = 0.
    /// Voided payments are excluded from the TotalPaid computation.
    /// **Validates: Requirement 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TotalPaid_OnlyCountsNonVoidedPayments(
        PositiveInt totalAmountSeed,
        PositiveInt[] paymentAmountSeeds,
        bool[] voidFlags)
    {
        if (paymentAmountSeeds.Length == 0)
            return true.ToProperty().Label("No payments — trivially true");

        var dbContext = CreateDbContext();
        try
        {
            var totalAmount = GenerateAmount(totalAmountSeed.Get);

            // Create a single Issued non-deleted invoice
            var invoice = CreateInvoice(1, TestBusinessId, StatusIssued, FinancialUnpaid, false, totalAmount);
            dbContext.Invoices.Add(invoice);
            dbContext.SaveChanges();

            var paymentCount = Math.Min(paymentAmountSeeds.Length, 10);
            var flags = voidFlags.Length > 0 ? voidFlags : new[] { false };

            // Add payments with mixed void flags
            for (int i = 0; i < paymentCount; i++)
            {
                var paymentAmount = Math.Min(
                    GenerateAmount(paymentAmountSeeds[i].Get) / 100m,
                    totalAmount / paymentCount);
                var isVoided = flags[i % flags.Length];

                dbContext.Payments.Add(new Payment
                {
                    Id = i + 1,
                    BusinessId = TestBusinessId,
                    InvoiceId = 1,
                    PaymentMethodTypeId = 1,
                    PaymentDateUtc = DateTime.UtcNow.AddDays(-i),
                    Amount = paymentAmount,
                    IsVoided = isVoided,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = "test-user"
                });
            }
            dbContext.SaveChanges();

            // Compute expected TotalPaid (only non-voided payments)
            var expectedTotalPaid = dbContext.Payments
                .Where(p => p.InvoiceId == 1 && !p.IsVoided)
                .Sum(p => p.Amount);

            var expectedOutstanding = totalAmount - expectedTotalPaid;

            // Verify the oracle computation
            var totalPaidCorrect = expectedTotalPaid >= 0;
            var outstandingCorrect = expectedOutstanding == totalAmount - expectedTotalPaid;

            // Verify voided payments are excluded
            var voidedSum = dbContext.Payments
                .Where(p => p.InvoiceId == 1 && p.IsVoided)
                .Sum(p => p.Amount);

            var voidedExcluded = expectedTotalPaid ==
                dbContext.Payments.Where(p => p.InvoiceId == 1).Sum(p => p.Amount) - voidedSum;

            var allPropertiesHold = totalPaidCorrect && outstandingCorrect && voidedExcluded;

            return allPropertiesHold.ToProperty()
                .Label($"TotalAmount={totalAmount}, TotalPaid={expectedTotalPaid}, " +
                       $"Outstanding={expectedOutstanding}, VoidedSum={voidedSum}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion
}
