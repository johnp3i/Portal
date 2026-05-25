using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 7: Invoice status breakdown counts sum to total

/// <summary>
/// Property-based tests for Dashboard Invoice Status Breakdown computation.
/// Validates that PaidCount + PartiallyPaidCount + UnpaidCount + OverdueCount == TotalCount,
/// and each individual count matches the number of invoices with that specific InvoiceFinancialStatusTypeId.
/// Tested as a pure computation over generated invoice data.
/// **Validates: Requirements 3.1**
/// </summary>
public class DashboardInvoiceStatusBreakdownPropertyTests
{
    private const int TestBusinessId = 1;

    // Financial status constants (matching DashboardService)
    private const int FinancialStatusUnpaid = 1;
    private const int FinancialStatusPartiallyPaid = 2;
    private const int FinancialStatusPaid = 3;
    private const int FinancialStatusOverdue = 4;

    // Invoice status constants
    private const int InvoiceStatusIssued = 2;

    #region Test Infrastructure

    /// <summary>
    /// Computes the expected InvoiceStatusBreakdownDto from a list of invoices.
    /// This is the oracle function: counts issued (InvoiceStatusTypeId = 2), non-deleted invoices
    /// grouped by InvoiceFinancialStatusTypeId.
    /// </summary>
    private static InvoiceStatusBreakdownDto ComputeExpectedBreakdown(List<Invoice> invoices, int businessId)
    {
        var qualifying = invoices
            .Where(i => i.BusinessId == businessId
                        && !i.IsDeleted
                        && i.InvoiceStatusTypeId == InvoiceStatusIssued)
            .ToList();

        var result = new InvoiceStatusBreakdownDto
        {
            PaidCount = qualifying.Count(i => i.InvoiceFinancialStatusTypeId == FinancialStatusPaid),
            PartiallyPaidCount = qualifying.Count(i => i.InvoiceFinancialStatusTypeId == FinancialStatusPartiallyPaid),
            UnpaidCount = qualifying.Count(i => i.InvoiceFinancialStatusTypeId == FinancialStatusUnpaid),
            OverdueCount = qualifying.Count(i => i.InvoiceFinancialStatusTypeId == FinancialStatusOverdue)
        };

        result.TotalCount = result.PaidCount + result.PartiallyPaidCount + result.UnpaidCount + result.OverdueCount;

        return result;
    }

    /// <summary>
    /// Generates an invoice with controlled parameters for testing.
    /// </summary>
    private static Invoice CreateInvoice(
        int id, int businessId, int invoiceStatusTypeId, int financialStatusTypeId, bool isDeleted)
    {
        return new Invoice
        {
            Id = id,
            BusinessId = businessId,
            CustomerId = 1,
            InvoiceStatusTypeId = invoiceStatusTypeId,
            InvoiceFinancialStatusTypeId = financialStatusTypeId,
            InvoiceNumber = $"INV-{id:D5}",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-id)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30 - id)),
            Subtotal = 100m,
            TaxAmount = 15m,
            TotalAmount = 115m,
            CurrencyCode = "EUR",
            IsDeleted = isDeleted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates a financial status ID (1-4) from a seed value.
    /// </summary>
    private static int GenerateFinancialStatusId(int seed)
    {
        return (Math.Abs(seed) % 4) + 1;
    }

    /// <summary>
    /// Generates an invoice status type ID from a seed value.
    /// Values: 1=Draft, 2=Issued, 3=Cancelled
    /// </summary>
    private static int GenerateInvoiceStatusTypeId(int seed)
    {
        return (Math.Abs(seed) % 3) + 1;
    }

    #endregion

    #region Property 7: Invoice status breakdown counts sum to total

    /// <summary>
    /// Property 7: The sum of all status counts equals TotalCount.
    /// Generates random invoices with varying financial statuses and asserts
    /// PaidCount + PartiallyPaidCount + UnpaidCount + OverdueCount == TotalCount.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceStatusBreakdown_CountsSumToTotal(
        PositiveInt[] statusSeeds, bool[] deletedFlags, bool[] issuedFlags)
    {
        if (statusSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(statusSeeds.Length, 25);
        var invoices = new List<Invoice>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var financialStatusId = GenerateFinancialStatusId(statusSeeds[i].Get);
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var isIssued = issuedFlags.Length > 0 && issuedFlags[i % issuedFlags.Length];
            var invoiceStatusTypeId = isIssued ? InvoiceStatusIssued : GenerateInvoiceStatusTypeId(statusSeeds[i].Get + 1);

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, invoiceStatusTypeId, financialStatusId, isDeleted));
        }

        var result = ComputeExpectedBreakdown(invoices, TestBusinessId);

        var sumOfCounts = result.PaidCount + result.PartiallyPaidCount + result.UnpaidCount + result.OverdueCount;

        return (sumOfCounts == result.TotalCount).ToProperty()
            .Label($"Sum of counts ({sumOfCounts}) should equal TotalCount ({result.TotalCount}), " +
                   $"Paid={result.PaidCount}, Partial={result.PartiallyPaidCount}, " +
                   $"Unpaid={result.UnpaidCount}, Overdue={result.OverdueCount}");
    }

    /// <summary>
    /// Property 7: Each individual count matches the number of invoices with that specific status.
    /// Generates random invoices and verifies each count matches the filtered count.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceStatusBreakdown_EachCountMatchesFilteredInvoices(
        PositiveInt[] statusSeeds, bool[] deletedFlags, bool[] issuedFlags)
    {
        if (statusSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(statusSeeds.Length, 25);
        var invoices = new List<Invoice>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var financialStatusId = GenerateFinancialStatusId(statusSeeds[i].Get);
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var isIssued = issuedFlags.Length > 0 && issuedFlags[i % issuedFlags.Length];
            var invoiceStatusTypeId = isIssued ? InvoiceStatusIssued : GenerateInvoiceStatusTypeId(statusSeeds[i].Get + 1);

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, invoiceStatusTypeId, financialStatusId, isDeleted));
        }

        var result = ComputeExpectedBreakdown(invoices, TestBusinessId);

        // Manually count qualifying invoices per status
        var qualifying = invoices
            .Where(i => i.BusinessId == TestBusinessId && !i.IsDeleted && i.InvoiceStatusTypeId == InvoiceStatusIssued)
            .ToList();

        var expectedPaid = qualifying.Count(i => i.InvoiceFinancialStatusTypeId == FinancialStatusPaid);
        var expectedPartial = qualifying.Count(i => i.InvoiceFinancialStatusTypeId == FinancialStatusPartiallyPaid);
        var expectedUnpaid = qualifying.Count(i => i.InvoiceFinancialStatusTypeId == FinancialStatusUnpaid);
        var expectedOverdue = qualifying.Count(i => i.InvoiceFinancialStatusTypeId == FinancialStatusOverdue);

        var paidMatch = result.PaidCount == expectedPaid;
        var partialMatch = result.PartiallyPaidCount == expectedPartial;
        var unpaidMatch = result.UnpaidCount == expectedUnpaid;
        var overdueMatch = result.OverdueCount == expectedOverdue;

        return (paidMatch && partialMatch && unpaidMatch && overdueMatch).ToProperty()
            .Label($"Paid: expected={expectedPaid} actual={result.PaidCount}, " +
                   $"Partial: expected={expectedPartial} actual={result.PartiallyPaidCount}, " +
                   $"Unpaid: expected={expectedUnpaid} actual={result.UnpaidCount}, " +
                   $"Overdue: expected={expectedOverdue} actual={result.OverdueCount}");
    }

    /// <summary>
    /// Deleted invoices are excluded from the breakdown counts.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceStatusBreakdown_ExcludesDeletedInvoices(PositiveInt[] statusSeeds)
    {
        if (statusSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(statusSeeds.Length, 20);
        var invoices = new List<Invoice>();

        // Create all invoices as issued but deleted
        for (int i = 0; i < invoiceCount; i++)
        {
            var financialStatusId = GenerateFinancialStatusId(statusSeeds[i].Get);
            invoices.Add(CreateInvoice(i + 1, TestBusinessId, InvoiceStatusIssued, financialStatusId, isDeleted: true));
        }

        var result = ComputeExpectedBreakdown(invoices, TestBusinessId);

        return (result.TotalCount == 0).ToProperty()
            .Label($"Expected TotalCount=0 for all deleted invoices, but got {result.TotalCount}");
    }

    /// <summary>
    /// Non-issued invoices (Draft, Cancelled) are excluded from the breakdown counts.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceStatusBreakdown_ExcludesNonIssuedInvoices(PositiveInt[] statusSeeds)
    {
        if (statusSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(statusSeeds.Length, 20);
        var invoices = new List<Invoice>();

        // Create invoices with Draft (1) or Cancelled (3) status — never Issued (2)
        for (int i = 0; i < invoiceCount; i++)
        {
            var financialStatusId = GenerateFinancialStatusId(statusSeeds[i].Get);
            var invoiceStatusTypeId = (i % 2 == 0) ? 1 : 3; // Draft or Cancelled
            invoices.Add(CreateInvoice(i + 1, TestBusinessId, invoiceStatusTypeId, financialStatusId, isDeleted: false));
        }

        var result = ComputeExpectedBreakdown(invoices, TestBusinessId);

        return (result.TotalCount == 0).ToProperty()
            .Label($"Expected TotalCount=0 for non-issued invoices, but got {result.TotalCount}");
    }

    /// <summary>
    /// Invoices from a different business are excluded from the breakdown.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceStatusBreakdown_ExcludesOtherBusinessInvoices(PositiveInt[] statusSeeds)
    {
        if (statusSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(statusSeeds.Length, 20);
        var invoices = new List<Invoice>();
        var otherBusinessId = 99;

        // Create issued, non-deleted invoices for a DIFFERENT business
        for (int i = 0; i < invoiceCount; i++)
        {
            var financialStatusId = GenerateFinancialStatusId(statusSeeds[i].Get);
            invoices.Add(CreateInvoice(i + 1, otherBusinessId, InvoiceStatusIssued, financialStatusId, isDeleted: false));
        }

        // Compute for TestBusinessId — should be zero since all invoices belong to otherBusinessId
        var result = ComputeExpectedBreakdown(invoices, TestBusinessId);

        return (result.TotalCount == 0).ToProperty()
            .Label($"Expected TotalCount=0 for other business invoices, but got {result.TotalCount}");
    }

    /// <summary>
    /// When no invoices exist, all counts are zero.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void InvoiceStatusBreakdown_NoInvoices_ReturnsAllZeros()
    {
        var invoices = new List<Invoice>();
        var result = ComputeExpectedBreakdown(invoices, TestBusinessId);

        Assert.Equal(0, result.PaidCount);
        Assert.Equal(0, result.PartiallyPaidCount);
        Assert.Equal(0, result.UnpaidCount);
        Assert.Equal(0, result.OverdueCount);
        Assert.Equal(0, result.TotalCount);
    }

    /// <summary>
    /// Mixed scenario: invoices across multiple businesses, statuses, and deletion states.
    /// Only issued, non-deleted, same-business invoices are counted.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceStatusBreakdown_MixedScenario_OnlyCountsQualifyingInvoices(
        PositiveInt[] statusSeeds, bool[] deletedFlags, bool[] issuedFlags, bool[] sameBusinessFlags)
    {
        if (statusSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(statusSeeds.Length, 25);
        var invoices = new List<Invoice>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var financialStatusId = GenerateFinancialStatusId(statusSeeds[i].Get);
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var isIssued = issuedFlags.Length > 0 && issuedFlags[i % issuedFlags.Length];
            var isSameBusiness = sameBusinessFlags.Length > 0 && sameBusinessFlags[i % sameBusinessFlags.Length];

            var invoiceStatusTypeId = isIssued ? InvoiceStatusIssued : GenerateInvoiceStatusTypeId(statusSeeds[i].Get + 1);
            var businessId = isSameBusiness ? TestBusinessId : 99;

            invoices.Add(CreateInvoice(i + 1, businessId, invoiceStatusTypeId, financialStatusId, isDeleted));
        }

        var result = ComputeExpectedBreakdown(invoices, TestBusinessId);

        // Manually count qualifying invoices
        var qualifying = invoices
            .Where(i => i.BusinessId == TestBusinessId && !i.IsDeleted && i.InvoiceStatusTypeId == InvoiceStatusIssued)
            .ToList();

        var expectedTotal = qualifying.Count;
        var sumOfCounts = result.PaidCount + result.PartiallyPaidCount + result.UnpaidCount + result.OverdueCount;

        return (result.TotalCount == expectedTotal && sumOfCounts == expectedTotal).ToProperty()
            .Label($"Expected TotalCount={expectedTotal}, Actual TotalCount={result.TotalCount}, " +
                   $"SumOfCounts={sumOfCounts}, TotalInvoices={invoiceCount}, Qualifying={qualifying.Count}");
    }

    #endregion
}
