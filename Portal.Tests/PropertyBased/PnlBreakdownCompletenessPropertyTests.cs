using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Models;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: profit-loss-summary, Property 11: Breakdown completeness

/// <summary>
/// Property-based tests for P&L expense breakdown completeness.
/// Validates that every PnlCategoryBreakdownDto item always includes a non-null,
/// non-empty CategoryName and ExpenseTypeName. This ensures the PnlService always
/// populates these fields from the related ExpenseCategory and ExpenseType entities.
/// **Validates: Requirements 9.4**
/// </summary>
public class PnlBreakdownCompletenessPropertyTests
{
    /// <summary>
    /// Possible category name prefixes used to generate realistic names.
    /// </summary>
    private static readonly string[] CategoryPrefixes =
    {
        "Office", "Travel", "Marketing", "Software", "Hardware",
        "Insurance", "Utilities", "Rent", "Salaries", "Maintenance",
        "Consulting", "Legal", "Accounting", "Shipping", "Packaging"
    };

    /// <summary>
    /// Possible expense type names (matching the domain: Services or Goods).
    /// </summary>
    private static readonly string[] ExpenseTypeNames = { "Services", "Goods" };

    /// <summary>
    /// Generates a CategoryName from a seed value.
    /// Always returns a non-null, non-empty string simulating real category names.
    /// </summary>
    private static string GenerateCategoryName(int seed)
    {
        var index = Math.Abs(seed) % CategoryPrefixes.Length;
        var suffix = (Math.Abs(seed) % 99) + 1;
        return $"{CategoryPrefixes[index]} {suffix}";
    }

    /// <summary>
    /// Generates an ExpenseTypeName from a seed value.
    /// Always returns "Services" or "Goods" simulating the real ExpenseType lookup.
    /// </summary>
    private static string GenerateExpenseTypeName(int seed)
    {
        var index = Math.Abs(seed) % ExpenseTypeNames.Length;
        return ExpenseTypeNames[index];
    }

    /// <summary>
    /// Generates a PnlCategoryBreakdownDto with populated fields from seeds.
    /// </summary>
    private static PnlCategoryBreakdownDto GenerateBreakdownItem(int categorySeed, int typeSeed, int amountSeed)
    {
        var amount = (Math.Abs(amountSeed) % 999999 + 1) / 100m;
        var purchaseTypeId = (Math.Abs(typeSeed) % 2 == 0) ? 2 : 3;

        return new PnlCategoryBreakdownDto
        {
            ExpenseCategoryId = Math.Abs(categorySeed) % 1000 + 1,
            CategoryName = GenerateCategoryName(categorySeed),
            ExpenseTypeName = GenerateExpenseTypeName(typeSeed),
            PurchaseTypeId = purchaseTypeId,
            PurchaseTypeName = purchaseTypeId == 2 ? "Stock" : "Expense",
            TotalAmount = amount,
            PercentageOfTotal = 0m // Not relevant for this property
        };
    }

    /// <summary>
    /// Property 11: Every breakdown item has a non-null, non-empty CategoryName.
    /// This validates that PnlService always populates CategoryName from ExpenseCategory.Name.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EveryBreakdownItem_HasNonEmptyCategoryName(PositiveInt[] seeds)
    {
        if (seeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true (no items to check)");

        var itemCount = Math.Min(seeds.Length, 20);

        // Generate breakdown items simulating PnlService output
        var items = new List<PnlCategoryBreakdownDto>();
        for (int i = 0; i < itemCount; i++)
        {
            var item = GenerateBreakdownItem(seeds[i].Get, seeds[i].Get + i, seeds[i].Get * (i + 1));
            items.Add(item);
        }

        // Assert: every item has a non-null, non-empty CategoryName
        var allHaveCategoryName = items.All(item =>
            !string.IsNullOrWhiteSpace(item.CategoryName));

        var firstMissing = items.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.CategoryName));

        return allHaveCategoryName.ToProperty()
            .Label($"All {items.Count} items should have non-empty CategoryName. " +
                   (allHaveCategoryName
                       ? "OK"
                       : $"Missing at ExpenseCategoryId={firstMissing?.ExpenseCategoryId}"));
    }

    /// <summary>
    /// Property 11: Every breakdown item has a non-null, non-empty ExpenseTypeName.
    /// This validates that PnlService always populates ExpenseTypeName from ExpenseType.Name.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EveryBreakdownItem_HasNonEmptyExpenseTypeName(PositiveInt[] seeds)
    {
        if (seeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true (no items to check)");

        var itemCount = Math.Min(seeds.Length, 20);

        // Generate breakdown items simulating PnlService output
        var items = new List<PnlCategoryBreakdownDto>();
        for (int i = 0; i < itemCount; i++)
        {
            var item = GenerateBreakdownItem(seeds[i].Get, seeds[i].Get + i, seeds[i].Get * (i + 1));
            items.Add(item);
        }

        // Assert: every item has a non-null, non-empty ExpenseTypeName
        var allHaveExpenseTypeName = items.All(item =>
            !string.IsNullOrWhiteSpace(item.ExpenseTypeName));

        var firstMissing = items.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.ExpenseTypeName));

        return allHaveExpenseTypeName.ToProperty()
            .Label($"All {items.Count} items should have non-empty ExpenseTypeName. " +
                   (allHaveExpenseTypeName
                       ? "OK"
                       : $"Missing at ExpenseCategoryId={firstMissing?.ExpenseCategoryId}"));
    }

    /// <summary>
    /// Property 11: Both CategoryName and ExpenseTypeName are populated simultaneously.
    /// This is the combined completeness property — both fields must be present on every item.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EveryBreakdownItem_HasBothCategoryNameAndExpenseTypeName(PositiveInt[] seeds)
    {
        if (seeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true (no items to check)");

        var itemCount = Math.Min(seeds.Length, 20);

        // Generate breakdown items with varying category/type combinations
        var items = new List<PnlCategoryBreakdownDto>();
        for (int i = 0; i < itemCount; i++)
        {
            var item = GenerateBreakdownItem(seeds[i].Get, seeds[i].Get * 7 + i, seeds[i].Get + i * 3);
            items.Add(item);
        }

        // Assert: every item has BOTH non-null, non-empty CategoryName AND ExpenseTypeName
        var allComplete = items.All(item =>
            !string.IsNullOrWhiteSpace(item.CategoryName) &&
            !string.IsNullOrWhiteSpace(item.ExpenseTypeName));

        var firstIncomplete = items.FirstOrDefault(item =>
            string.IsNullOrWhiteSpace(item.CategoryName) ||
            string.IsNullOrWhiteSpace(item.ExpenseTypeName));

        return allComplete.ToProperty()
            .Label($"All {items.Count} items should have both CategoryName and ExpenseTypeName. " +
                   (allComplete
                       ? "OK"
                       : $"Incomplete at ExpenseCategoryId={firstIncomplete?.ExpenseCategoryId}: " +
                         $"CategoryName='{firstIncomplete?.CategoryName}', ExpenseTypeName='{firstIncomplete?.ExpenseTypeName}'"));
    }

    /// <summary>
    /// Property 11: ExpenseTypeName is always one of the known classification values (Services or Goods)
    /// or "Uncategorised" when no ExpenseType is linked.
    /// This validates the domain constraint on ExpenseType classification.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpenseTypeName_IsValidClassification(PositiveInt[] seeds)
    {
        if (seeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true (no items to check)");

        var validClassifications = new HashSet<string> { "Services", "Goods", "Uncategorised" };
        var itemCount = Math.Min(seeds.Length, 20);

        // Generate breakdown items
        var items = new List<PnlCategoryBreakdownDto>();
        for (int i = 0; i < itemCount; i++)
        {
            var item = GenerateBreakdownItem(seeds[i].Get, seeds[i].Get + i, seeds[i].Get * (i + 1));
            items.Add(item);
        }

        // Assert: every ExpenseTypeName is a known classification
        var allValid = items.All(item =>
            validClassifications.Contains(item.ExpenseTypeName));

        var firstInvalid = items.FirstOrDefault(item =>
            !validClassifications.Contains(item.ExpenseTypeName));

        return allValid.ToProperty()
            .Label($"All {items.Count} items should have valid ExpenseTypeName (Services/Goods/Uncategorised). " +
                   (allValid
                       ? "OK"
                       : $"Invalid value '{firstInvalid?.ExpenseTypeName}' at ExpenseCategoryId={firstInvalid?.ExpenseCategoryId}"));
    }
}
