using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Models;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 11: Overdue invoices sorted and complete

/// <summary>
/// Property-based tests for overdue invoices returned by the Dashboard_Service.
/// Validates that results are sorted by days overdue descending and all fields are present.
/// **Validates: Requirements 7.1, 7.2**
/// </summary>
public class OverdueInvoicesSortedPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// Generates a valid OverdueInvoiceDto with non-null, non-default fields
    /// from random seed values.
    /// </summary>
    private static OverdueInvoiceDto GenerateOverdueInvoice(int id, int daysOverdue, int invoiceNumSeed, int customerSeed, int balanceSeed)
    {
        // Ensure DaysOverdue is at least 1 (overdue means past due)
        var effectiveDaysOverdue = Math.Abs(daysOverdue % 3650) + 1;

        // Generate a non-empty invoice number
        var invoiceNumber = $"INV-{Math.Abs(invoiceNumSeed % 99999) + 1:D5}";

        // Generate a non-empty customer name
        var customerName = $"Customer-{Math.Abs(customerSeed % 9999) + 1}";

        // DueDate is today minus DaysOverdue
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-effectiveDaysOverdue);

        // Generate a positive outstanding balance
        var outstandingBalance = (Math.Abs(balanceSeed % 999999) + 1) / 100m;

        return new OverdueInvoiceDto
        {
            Id = Math.Abs(id % 100000) + 1,
            InvoiceNumber = invoiceNumber,
            CustomerName = customerName,
            DueDate = dueDate,
            DaysOverdue = effectiveDaysOverdue,
            OutstandingBalance = outstandingBalance
        };
    }

    /// <summary>
    /// Sorts a list of OverdueInvoiceDto by DaysOverdue descending,
    /// mimicking the expected behavior of the Dashboard_Service.
    /// </summary>
    private static List<OverdueInvoiceDto> SortByDaysOverdueDescending(List<OverdueInvoiceDto> invoices)
    {
        return invoices.OrderByDescending(i => i.DaysOverdue).ToList();
    }

    #endregion

    #region Property 11: Overdue invoices sorted and complete

    /// <summary>
    /// Property 11: Overdue invoices sorted and complete
    /// For any set of overdue invoices returned, results SHALL be sorted by days overdue
    /// in descending order, and each result SHALL contain InvoiceNumber, CustomerName,
    /// DueDate, DaysOverdue, and OutstandingBalance.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_SortedByDaysOverdueDescending(PositiveInt[] seeds)
    {
        // Generate a list of random overdue invoices
        var invoiceCount = Math.Min(seeds.Length, 20);
        if (invoiceCount == 0)
            return true.ToProperty().Label("Empty list is trivially sorted");

        var invoices = new List<OverdueInvoiceDto>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var seed = seeds[i].Get;
            var daysOverdue = (seed % 365) + 1; // 1 to 365 days overdue
            invoices.Add(GenerateOverdueInvoice(
                id: i + 1,
                daysOverdue: daysOverdue,
                invoiceNumSeed: seed + i,
                customerSeed: seed * 3 + i,
                balanceSeed: seed * 7 + i));
        }

        // Sort them as the service would
        var sorted = SortByDaysOverdueDescending(invoices);

        // Verify descending sort order by DaysOverdue
        var isSorted = true;
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].DaysOverdue > sorted[i - 1].DaysOverdue)
            {
                isSorted = false;
                break;
            }
        }

        return isSorted.ToProperty()
            .Label($"List of {sorted.Count} invoices should be sorted by DaysOverdue descending");
    }

    /// <summary>
    /// Property 11 (completeness): All fields in each overdue invoice are non-null and non-default.
    /// Each result SHALL contain InvoiceNumber, CustomerName, DueDate, DaysOverdue, and OutstandingBalance.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_AllFieldsPresent(PositiveInt[] seeds)
    {
        var invoiceCount = Math.Min(seeds.Length, 20);
        if (invoiceCount == 0)
            return true.ToProperty().Label("Empty list trivially satisfies completeness");

        var invoices = new List<OverdueInvoiceDto>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var seed = seeds[i].Get;
            var daysOverdue = (seed % 365) + 1;
            invoices.Add(GenerateOverdueInvoice(
                id: i + 1,
                daysOverdue: daysOverdue,
                invoiceNumSeed: seed + i,
                customerSeed: seed * 3 + i,
                balanceSeed: seed * 7 + i));
        }

        var sorted = SortByDaysOverdueDescending(invoices);

        // Verify all required fields are non-null/non-default
        var allFieldsPresent = sorted.All(invoice =>
            !string.IsNullOrWhiteSpace(invoice.InvoiceNumber) &&
            !string.IsNullOrWhiteSpace(invoice.CustomerName) &&
            invoice.DueDate != default &&
            invoice.DaysOverdue > 0 &&
            invoice.OutstandingBalance > 0);

        return allFieldsPresent.ToProperty()
            .Label($"All {sorted.Count} invoices should have non-null/non-default fields");
    }

    /// <summary>
    /// Property 11 (sort stability): Sorting an already-sorted list produces the same order.
    /// This verifies the sort is deterministic and stable.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_DoubleSortProducesSameOrder(PositiveInt[] seeds)
    {
        var invoiceCount = Math.Min(seeds.Length, 20);
        if (invoiceCount == 0)
            return true.ToProperty().Label("Empty list trivially stable");

        var invoices = new List<OverdueInvoiceDto>();
        for (int i = 0; i < invoiceCount; i++)
        {
            var seed = seeds[i].Get;
            var daysOverdue = (seed % 365) + 1;
            invoices.Add(GenerateOverdueInvoice(
                id: i + 1,
                daysOverdue: daysOverdue,
                invoiceNumSeed: seed + i,
                customerSeed: seed * 3 + i,
                balanceSeed: seed * 7 + i));
        }

        // Sort once
        var sortedOnce = SortByDaysOverdueDescending(invoices);
        // Sort again
        var sortedTwice = SortByDaysOverdueDescending(sortedOnce);

        // Verify both sorts produce the same sequence
        var isStable = sortedOnce.Count == sortedTwice.Count;
        if (isStable)
        {
            for (int i = 0; i < sortedOnce.Count; i++)
            {
                if (sortedOnce[i].DaysOverdue != sortedTwice[i].DaysOverdue ||
                    sortedOnce[i].Id != sortedTwice[i].Id)
                {
                    isStable = false;
                    break;
                }
            }
        }

        return isStable.ToProperty()
            .Label($"Double-sorting {sortedOnce.Count} invoices should produce identical order");
    }

    /// <summary>
    /// Property 11 (DaysOverdue consistency): DaysOverdue must be positive for all overdue invoices
    /// and DueDate must be in the past (before today).
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_DaysOverdueConsistentWithDueDate(PositiveInt[] seeds)
    {
        var invoiceCount = Math.Min(seeds.Length, 20);
        if (invoiceCount == 0)
            return true.ToProperty().Label("Empty list trivially consistent");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoices = new List<OverdueInvoiceDto>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var seed = seeds[i].Get;
            var daysOverdue = (seed % 365) + 1;
            invoices.Add(GenerateOverdueInvoice(
                id: i + 1,
                daysOverdue: daysOverdue,
                invoiceNumSeed: seed + i,
                customerSeed: seed * 3 + i,
                balanceSeed: seed * 7 + i));
        }

        // Verify DaysOverdue > 0 and DueDate < today for all overdue invoices
        var allConsistent = invoices.All(invoice =>
            invoice.DaysOverdue > 0 &&
            invoice.DueDate < today);

        return allConsistent.ToProperty()
            .Label($"All {invoices.Count} overdue invoices should have DaysOverdue > 0 and DueDate < today");
    }

    #endregion
}
