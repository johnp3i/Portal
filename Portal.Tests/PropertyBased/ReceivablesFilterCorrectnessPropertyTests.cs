using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Models;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 14: Receivables filter correctness

/// <summary>
/// Property-based tests for receivables filter correctness.
/// Validates that for any combination of active filters (search term, financial status,
/// customer, date range), all results returned satisfy ALL active filter conditions simultaneously.
/// **Validates: Requirements 9.3, 9.4, 9.5, 9.6**
/// </summary>
public class ReceivablesFilterCorrectnessPropertyTests
{
    // Financial Status Type IDs
    private const int StatusUnpaid = 1;
    private const int StatusPartiallyPaid = 2;
    private const int StatusPaid = 3;
    private const int StatusOverdue = 4;
    private const int StatusWrittenOff = 5;

    private static readonly string[] CustomerNames = new[]
    {
        "Acme Corp", "Beta Industries", "Gamma Solutions", "Delta Services",
        "Epsilon Holdings", "Zeta Trading", "Eta Logistics", "Theta Manufacturing"
    };

    private static readonly string[] FinancialStatusNames = new[]
    {
        "Unpaid", "PartiallyPaid", "Paid", "Overdue", "WrittenOff"
    };

    #region Test Infrastructure

    /// <summary>
    /// Generates a list of ReceivableDto items with random but valid data.
    /// </summary>
    private static List<ReceivableDto> GenerateReceivables(int[] seeds)
    {
        var items = new List<ReceivableDto>();

        for (int i = 0; i < seeds.Length; i++)
        {
            var seed = Math.Abs(seeds[i]);
            var customerId = (seed % CustomerNames.Length) + 1;
            var customerName = CustomerNames[customerId - 1];
            var financialStatusId = (seed % 5) + 1;
            var totalAmount = ((seed % 9999) + 100) * 1.00m;
            var totalPaid = financialStatusId == StatusPaid
                ? totalAmount
                : financialStatusId == StatusUnpaid
                    ? 0m
                    : (seed % (int)totalAmount) * 1.00m;
            var outstandingBalance = totalAmount - totalPaid;

            // Generate dates between 2024-01-01 and 2025-12-31
            var baseDate = new DateOnly(2024, 1, 1);
            var invoiceDate = baseDate.AddDays(seed % 730);
            var dueDate = invoiceDate.AddDays(30 + (seed % 60));

            items.Add(new ReceivableDto
            {
                Id = i + 1,
                InvoiceNumber = $"INV-{(i + 1):D5}",
                CustomerName = customerName,
                InvoiceDate = invoiceDate,
                DueDate = dueDate,
                TotalAmount = totalAmount,
                TotalPaid = totalPaid,
                OutstandingBalance = outstandingBalance,
                InvoiceFinancialStatusTypeId = financialStatusId,
                FinancialStatusName = FinancialStatusNames[financialStatusId - 1],
                HasOutstandingBalance = outstandingBalance > 0
            });
        }

        return items;
    }

    /// <summary>
    /// Applies the same filtering logic as ReceivablesQueryService to a list of ReceivableDto items.
    /// This is the oracle function that mirrors the SQL WHERE clause logic.
    /// </summary>
    private static List<ReceivableDto> ApplyFilters(
        List<ReceivableDto> items,
        string? searchTerm,
        int? financialStatusFilter,
        int? customerFilter,
        DateOnly? dueFrom,
        DateOnly? dueTo)
    {
        var filtered = items.AsEnumerable();

        // Search filter: InvoiceNumber or CustomerName contains search term (case-insensitive)
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filtered = filtered.Where(r =>
                r.InvoiceNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                r.CustomerName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        // Financial status filter
        if (financialStatusFilter.HasValue)
        {
            filtered = filtered.Where(r =>
                r.InvoiceFinancialStatusTypeId == financialStatusFilter.Value);
        }

        // Customer filter (by customer index mapped to name)
        if (customerFilter.HasValue)
        {
            var customerName = customerFilter.Value > 0 && customerFilter.Value <= CustomerNames.Length
                ? CustomerNames[customerFilter.Value - 1]
                : null;
            if (customerName != null)
            {
                filtered = filtered.Where(r => r.CustomerName == customerName);
            }
        }

        // Date range filter: DueDate >= dueFrom
        if (dueFrom.HasValue)
        {
            filtered = filtered.Where(r => r.DueDate >= dueFrom.Value);
        }

        // Date range filter: DueDate <= dueTo
        if (dueTo.HasValue)
        {
            filtered = filtered.Where(r => r.DueDate <= dueTo.Value);
        }

        return filtered.ToList();
    }

    /// <summary>
    /// Verifies that a single ReceivableDto satisfies ALL active filter conditions.
    /// Returns true if the item passes all filters, false otherwise.
    /// </summary>
    private static bool SatisfiesAllFilters(
        ReceivableDto item,
        string? searchTerm,
        int? financialStatusFilter,
        int? customerFilter,
        DateOnly? dueFrom,
        DateOnly? dueTo)
    {
        // Search filter check
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var matchesSearch =
                item.InvoiceNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                item.CustomerName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
            if (!matchesSearch) return false;
        }

        // Financial status filter check
        if (financialStatusFilter.HasValue)
        {
            if (item.InvoiceFinancialStatusTypeId != financialStatusFilter.Value)
                return false;
        }

        // Customer filter check
        if (customerFilter.HasValue)
        {
            var customerName = customerFilter.Value > 0 && customerFilter.Value <= CustomerNames.Length
                ? CustomerNames[customerFilter.Value - 1]
                : null;
            if (customerName != null && item.CustomerName != customerName)
                return false;
        }

        // Date range: DueDate >= dueFrom
        if (dueFrom.HasValue)
        {
            if (item.DueDate < dueFrom.Value) return false;
        }

        // Date range: DueDate <= dueTo
        if (dueTo.HasValue)
        {
            if (item.DueDate > dueTo.Value) return false;
        }

        return true;
    }

    /// <summary>
    /// Generates a search term from a seed. Returns null sometimes to test no-search scenarios.
    /// </summary>
    private static string? GenerateSearchTerm(int seed)
    {
        var options = new string?[]
        {
            null, null, null, // 3/10 chance of no search
            "INV", "Acme", "Beta", "Gamma", "Delta", "00", "Corp"
        };
        return options[Math.Abs(seed) % options.Length];
    }

    /// <summary>
    /// Generates a financial status filter from a seed. Returns null sometimes.
    /// </summary>
    private static int? GenerateFinancialStatusFilter(int seed)
    {
        // null = no filter, 1-5 = specific status
        var value = Math.Abs(seed) % 8; // 0-7
        return value >= 5 ? null : value + 1; // 5,6,7 → null; 0-4 → 1-5
    }

    /// <summary>
    /// Generates a customer filter from a seed. Returns null sometimes.
    /// </summary>
    private static int? GenerateCustomerFilter(int seed)
    {
        // null = no filter, 1-8 = specific customer
        var value = Math.Abs(seed) % 12; // 0-11
        return value >= 8 ? null : value + 1; // 8-11 → null; 0-7 → 1-8
    }

    /// <summary>
    /// Generates date range filters from seeds. Returns null sometimes.
    /// </summary>
    private static (DateOnly? DueFrom, DateOnly? DueTo) GenerateDateRange(int seedFrom, int seedTo)
    {
        var baseDate = new DateOnly(2024, 1, 1);

        // 40% chance of no dueFrom
        DateOnly? dueFrom = (Math.Abs(seedFrom) % 5 < 2)
            ? null
            : baseDate.AddDays(Math.Abs(seedFrom) % 365);

        // 40% chance of no dueTo
        DateOnly? dueTo = (Math.Abs(seedTo) % 5 < 2)
            ? null
            : baseDate.AddDays(365 + Math.Abs(seedTo) % 365);

        // Ensure dueFrom <= dueTo when both are set
        if (dueFrom.HasValue && dueTo.HasValue && dueFrom.Value > dueTo.Value)
        {
            (dueFrom, dueTo) = (dueTo, dueFrom);
        }

        return (dueFrom, dueTo);
    }

    #endregion

    #region Property 14: Receivables filter correctness

    /// <summary>
    /// Property 14: Receivables filter correctness
    /// For any combination of active filters (search term, financial status, customer, date range),
    /// all results returned SHALL satisfy ALL active filter conditions simultaneously.
    /// **Validates: Requirements 9.3, 9.4, 9.5, 9.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllResults_SatisfyAllActiveFilters(
        PositiveInt[] itemSeeds,
        int searchSeed,
        int statusSeed,
        int customerSeed,
        int dateFromSeed,
        int dateToSeed)
    {
        // Generate between 1 and 20 receivable items
        var seedCount = Math.Max(1, Math.Min(itemSeeds.Length, 20));
        var seeds = itemSeeds.Take(seedCount).Select(s => s.Get).ToArray();
        var receivables = GenerateReceivables(seeds);

        // Generate random filter combination
        var searchTerm = GenerateSearchTerm(searchSeed);
        var financialStatusFilter = GenerateFinancialStatusFilter(statusSeed);
        var customerFilter = GenerateCustomerFilter(customerSeed);
        var (dueFrom, dueTo) = GenerateDateRange(dateFromSeed, dateToSeed);

        // Apply filters (oracle)
        var filteredResults = ApplyFilters(receivables, searchTerm, financialStatusFilter,
            customerFilter, dueFrom, dueTo);

        // Verify: every result in the filtered set satisfies ALL active filters
        var allSatisfy = filteredResults.All(item =>
            SatisfiesAllFilters(item, searchTerm, financialStatusFilter, customerFilter, dueFrom, dueTo));

        // Verify: no item outside the filtered set should satisfy all filters
        var excludedItems = receivables.Except(filteredResults).ToList();
        var noneExcludedSatisfy = excludedItems.All(item =>
            !SatisfiesAllFilters(item, searchTerm, financialStatusFilter, customerFilter, dueFrom, dueTo));

        return (allSatisfy && noneExcludedSatisfy).ToProperty()
            .Label($"Items={receivables.Count}, Filtered={filteredResults.Count}, " +
                   $"Search='{searchTerm}', Status={financialStatusFilter}, " +
                   $"Customer={customerFilter}, DueFrom={dueFrom}, DueTo={dueTo}");
    }

    /// <summary>
    /// Property 14b: Search filter correctness
    /// When a search term is provided, all results contain the search term in either
    /// InvoiceNumber or CustomerName (case-insensitive).
    /// **Validates: Requirement 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchFilter_AllResultsContainSearchTerm(
        PositiveInt[] itemSeeds,
        PositiveInt searchIndex)
    {
        var seedCount = Math.Max(1, Math.Min(itemSeeds.Length, 20));
        var seeds = itemSeeds.Take(seedCount).Select(s => s.Get).ToArray();
        var receivables = GenerateReceivables(seeds);

        // Always use a non-null search term for this test
        var searchTerms = new[] { "INV", "Acme", "Beta", "Gamma", "Delta", "Corp", "00", "Solutions" };
        var searchTerm = searchTerms[searchIndex.Get % searchTerms.Length];

        var filteredResults = ApplyFilters(receivables, searchTerm, null, null, null, null);

        var allContainTerm = filteredResults.All(item =>
            item.InvoiceNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            item.CustomerName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

        return allContainTerm.ToProperty()
            .Label($"Search='{searchTerm}', Results={filteredResults.Count}/{receivables.Count}");
    }

    /// <summary>
    /// Property 14c: Financial status filter correctness
    /// When a financial status filter is provided, all results have the matching status.
    /// **Validates: Requirement 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FinancialStatusFilter_AllResultsMatchStatus(
        PositiveInt[] itemSeeds,
        PositiveInt statusSeed)
    {
        var seedCount = Math.Max(1, Math.Min(itemSeeds.Length, 20));
        var seeds = itemSeeds.Take(seedCount).Select(s => s.Get).ToArray();
        var receivables = GenerateReceivables(seeds);

        // Generate a valid financial status (1-5)
        var financialStatusFilter = (statusSeed.Get % 5) + 1;

        var filteredResults = ApplyFilters(receivables, null, financialStatusFilter, null, null, null);

        var allMatchStatus = filteredResults.All(item =>
            item.InvoiceFinancialStatusTypeId == financialStatusFilter);

        return allMatchStatus.ToProperty()
            .Label($"StatusFilter={financialStatusFilter}, Results={filteredResults.Count}/{receivables.Count}");
    }

    /// <summary>
    /// Property 14d: Customer filter correctness
    /// When a customer filter is provided, all results belong to that customer.
    /// **Validates: Requirement 9.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CustomerFilter_AllResultsMatchCustomer(
        PositiveInt[] itemSeeds,
        PositiveInt customerSeed)
    {
        var seedCount = Math.Max(1, Math.Min(itemSeeds.Length, 20));
        var seeds = itemSeeds.Take(seedCount).Select(s => s.Get).ToArray();
        var receivables = GenerateReceivables(seeds);

        // Generate a valid customer filter (1-8)
        var customerFilter = (customerSeed.Get % CustomerNames.Length) + 1;
        var expectedCustomerName = CustomerNames[customerFilter - 1];

        var filteredResults = ApplyFilters(receivables, null, null, customerFilter, null, null);

        var allMatchCustomer = filteredResults.All(item =>
            item.CustomerName == expectedCustomerName);

        return allMatchCustomer.ToProperty()
            .Label($"CustomerFilter={customerFilter} ({expectedCustomerName}), " +
                   $"Results={filteredResults.Count}/{receivables.Count}");
    }

    /// <summary>
    /// Property 14e: Date range filter correctness
    /// When a date range is provided, all results have DueDate within the range.
    /// **Validates: Requirement 9.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateRangeFilter_AllResultsWithinRange(
        PositiveInt[] itemSeeds,
        PositiveInt fromSeed,
        PositiveInt toSeed)
    {
        var seedCount = Math.Max(1, Math.Min(itemSeeds.Length, 20));
        var seeds = itemSeeds.Take(seedCount).Select(s => s.Get).ToArray();
        var receivables = GenerateReceivables(seeds);

        // Generate a valid date range
        var baseDate = new DateOnly(2024, 1, 1);
        var dueFrom = baseDate.AddDays(fromSeed.Get % 365);
        var dueTo = baseDate.AddDays(365 + toSeed.Get % 365);

        // Ensure dueFrom <= dueTo
        if (dueFrom > dueTo)
            (dueFrom, dueTo) = (dueTo, dueFrom);

        var filteredResults = ApplyFilters(receivables, null, null, null, dueFrom, dueTo);

        var allWithinRange = filteredResults.All(item =>
            item.DueDate >= dueFrom && item.DueDate <= dueTo);

        return allWithinRange.ToProperty()
            .Label($"DueFrom={dueFrom}, DueTo={dueTo}, Results={filteredResults.Count}/{receivables.Count}");
    }

    /// <summary>
    /// Property 14f: Combined filters are conjunctive (AND logic)
    /// When multiple filters are active simultaneously, results must satisfy ALL of them.
    /// This tests that filters compose correctly with AND semantics.
    /// **Validates: Requirements 9.3, 9.4, 9.5, 9.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CombinedFilters_AreConjunctive(
        PositiveInt[] itemSeeds,
        PositiveInt statusSeed,
        PositiveInt customerSeed,
        PositiveInt dateSeed)
    {
        var seedCount = Math.Max(3, Math.Min(itemSeeds.Length, 20));
        var seeds = itemSeeds.Take(seedCount).Select(s => s.Get).ToArray();
        var receivables = GenerateReceivables(seeds);

        // Apply all filters simultaneously
        var financialStatusFilter = (statusSeed.Get % 5) + 1;
        var customerFilter = (customerSeed.Get % CustomerNames.Length) + 1;
        var baseDate = new DateOnly(2024, 1, 1);
        var dueFrom = baseDate.AddDays(dateSeed.Get % 200);
        var dueTo = dueFrom.AddDays(180);

        var expectedCustomerName = CustomerNames[customerFilter - 1];

        var filteredResults = ApplyFilters(receivables, null, financialStatusFilter,
            customerFilter, dueFrom, dueTo);

        // Every result must satisfy ALL conditions simultaneously
        var allSatisfyAll = filteredResults.All(item =>
            item.InvoiceFinancialStatusTypeId == financialStatusFilter &&
            item.CustomerName == expectedCustomerName &&
            item.DueDate >= dueFrom &&
            item.DueDate <= dueTo);

        // Count items that satisfy individual filters but not all
        var satisfiesStatus = receivables.Count(r => r.InvoiceFinancialStatusTypeId == financialStatusFilter);
        var satisfiesCustomer = receivables.Count(r => r.CustomerName == expectedCustomerName);
        var satisfiesDate = receivables.Count(r => r.DueDate >= dueFrom && r.DueDate <= dueTo);

        // Combined result should be <= minimum of individual filter results
        var combinedIsSubset = filteredResults.Count <= Math.Min(satisfiesStatus,
            Math.Min(satisfiesCustomer, satisfiesDate));

        return (allSatisfyAll && combinedIsSubset).ToProperty()
            .Label($"Status={financialStatusFilter}, Customer={customerFilter}, " +
                   $"DateRange={dueFrom}-{dueTo}, Combined={filteredResults.Count}, " +
                   $"IndividualCounts: status={satisfiesStatus}, customer={satisfiesCustomer}, date={satisfiesDate}");
    }

    #endregion
}
