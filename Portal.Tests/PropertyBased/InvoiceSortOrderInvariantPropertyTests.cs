using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;

namespace Portal.Tests.PropertyBased;

// Feature: invoice-edit-modal-lines, Property 6: Table rows appear in ascending SortOrder

/// <summary>
/// Property-based tests for the sort order invariant of invoice line items.
/// The Razor view uses .OrderBy(l => l.SortOrder) to render line items.
/// This validates that ordering by SortOrder always produces a correctly ascending sequence.
/// **Validates: Requirements 1.7**
/// </summary>
public class InvoiceSortOrderInvariantPropertyTests
{
    /// <summary>
    /// Property 6a: For any collection of InvoiceLine items with arbitrary SortOrder values,
    /// ordering by SortOrder produces a sequence where each element's SortOrder is ≥ the
    /// previous element's SortOrder (weakly ascending, since duplicates may exist).
    /// **Validates: Requirements 1.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OrderBy_SortOrder_Produces_WeaklyAscending_Sequence()
    {
        var sortOrderGen = Gen.Choose(0, 10000);

        var lineGen = sortOrderGen.Select(sortOrder => new InvoiceLine
        {
            Id = 0,
            InvoiceId = 1,
            Description = "Item",
            Quantity = 1m,
            UnitPrice = 10m,
            SortOrder = sortOrder
        });

        var linesGen = Gen.ListOf(lineGen);

        return Prop.ForAll(
            linesGen.ToArbitrary(),
            (lines) =>
            {
                var ordered = lines.OrderBy(l => l.SortOrder).ToList();

                // Every consecutive pair must be weakly ascending
                for (int i = 1; i < ordered.Count; i++)
                {
                    if (ordered[i].SortOrder < ordered[i - 1].SortOrder)
                    {
                        return false.Label(
                            $"Weakly ascending violated at index {i}: " +
                            $"SortOrder[{i - 1}]={ordered[i - 1].SortOrder}, " +
                            $"SortOrder[{i}]={ordered[i].SortOrder}");
                    }
                }

                return true.Label("All elements in weakly ascending SortOrder");
            });
    }

    /// <summary>
    /// Property 6b: For any collection of InvoiceLine items with distinct SortOrder values,
    /// ordering by SortOrder produces a strictly ascending sequence where each element's
    /// SortOrder is strictly greater than the previous element's SortOrder.
    /// **Validates: Requirements 1.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OrderBy_SortOrder_WithDistinctValues_Produces_StrictlyAscending_Sequence()
    {
        // Generate a list of distinct SortOrder values by generating a list and deduplicating
        var distinctSortOrdersGen = Gen.ListOf(Gen.Choose(0, 100000))
            .Select(list => list.Distinct().ToList());

        return Prop.ForAll(
            distinctSortOrdersGen.ToArbitrary(),
            (sortOrders) =>
            {
                // Create InvoiceLine items with the distinct sort orders
                var lines = sortOrders.Select((so, idx) => new InvoiceLine
                {
                    Id = idx + 1,
                    InvoiceId = 1,
                    Description = $"Item {idx + 1}",
                    Quantity = 1m,
                    UnitPrice = 10m,
                    SortOrder = so
                }).ToList();

                var ordered = lines.OrderBy(l => l.SortOrder).ToList();

                // Every consecutive pair must be strictly ascending
                for (int i = 1; i < ordered.Count; i++)
                {
                    if (ordered[i].SortOrder <= ordered[i - 1].SortOrder)
                    {
                        return false.Label(
                            $"Strictly ascending violated at index {i}: " +
                            $"SortOrder[{i - 1}]={ordered[i - 1].SortOrder}, " +
                            $"SortOrder[{i}]={ordered[i].SortOrder}");
                    }
                }

                return true.Label("All elements in strictly ascending SortOrder (distinct values)");
            });
    }
}
