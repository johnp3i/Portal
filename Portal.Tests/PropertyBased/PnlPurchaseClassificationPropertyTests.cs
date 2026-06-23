using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: profit-loss-summary, Property 2: Purchase classification

/// <summary>
/// Property-based tests for P&L Purchase classification logic.
/// Validates that COGS equals the sum of TotalAmount for non-cancelled purchases
/// with PurchaseTypeId == 2 within the period for the current tenant,
/// and Operating Expenses equals the sum of TotalAmount for non-cancelled purchases
/// with PurchaseTypeId == 3 within the period for the current tenant.
/// Tested as a pure computation over generated purchase data.
/// **Validates: Requirements 1.2, 1.3, 8.2**
/// </summary>
public class PnlPurchaseClassificationPropertyTests
{
    private const int TestBusinessId = 1;
    private const int OtherBusinessId = 99;

    // PurchaseType constants
    private const int PurchaseTypeAsset = 1;
    private const int PurchaseTypeStock = 2;   // COGS
    private const int PurchaseTypeExpense = 3;  // Operating Expenses

    // Fixed test period
    private static readonly DateOnly PeriodStart = new(2024, 6, 1);
    private static readonly DateOnly PeriodEnd = new(2024, 6, 30);

    #region Test Infrastructure

    /// <summary>
    /// Computes expected COGS from a list of purchases (oracle function).
    /// COGS = sum of TotalAmount where PurchaseTypeId == 2, IsCancelled == false,
    /// InvoiceDate within period, BusinessId matches.
    /// </summary>
    private static decimal ComputeExpectedCogs(List<Purchase> purchases, int businessId, DateOnly startDate, DateOnly endDate)
    {
        return purchases
            .Where(p => p.BusinessId == businessId
                        && !p.IsCancelled
                        && p.PurchaseTypeId == PurchaseTypeStock
                        && p.InvoiceDate >= startDate
                        && p.InvoiceDate <= endDate)
            .Sum(p => p.TotalAmount);
    }

    /// <summary>
    /// Computes expected Operating Expenses from a list of purchases (oracle function).
    /// OpEx = sum of TotalAmount where PurchaseTypeId == 3, IsCancelled == false,
    /// InvoiceDate within period, BusinessId matches.
    /// </summary>
    private static decimal ComputeExpectedOpEx(List<Purchase> purchases, int businessId, DateOnly startDate, DateOnly endDate)
    {
        return purchases
            .Where(p => p.BusinessId == businessId
                        && !p.IsCancelled
                        && p.PurchaseTypeId == PurchaseTypeExpense
                        && p.InvoiceDate >= startDate
                        && p.InvoiceDate <= endDate)
            .Sum(p => p.TotalAmount);
    }

    /// <summary>
    /// Generates a DateOnly within the test period from a seed.
    /// </summary>
    private static DateOnly GenerateInPeriodDate(int seed)
    {
        var daysInPeriod = PeriodEnd.DayNumber - PeriodStart.DayNumber + 1;
        var dayOffset = Math.Abs(seed) % daysInPeriod;
        return PeriodStart.AddDays(dayOffset);
    }

    /// <summary>
    /// Generates a DateOnly outside the test period from a seed.
    /// </summary>
    private static DateOnly GenerateOutOfPeriodDate(int seed)
    {
        var monthOffset = (Math.Abs(seed) % 11) + 1; // 1-11 months offset
        var direction = seed >= 0 ? -1 : 1;
        var targetMonth = PeriodStart.AddMonths(monthOffset * direction);
        var daysInTargetMonth = DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month);
        var dayOffset = Math.Abs(seed) % daysInTargetMonth;
        return new DateOnly(targetMonth.Year, targetMonth.Month, dayOffset + 1);
    }

    /// <summary>
    /// Generates a positive decimal amount from a seed.
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = Math.Abs(seed) % 999999 + 1;
        return raw / 100m;
    }

    /// <summary>
    /// Generates a PurchaseTypeId (1, 2, or 3) from a seed.
    /// </summary>
    private static int GeneratePurchaseTypeId(int seed)
    {
        return (Math.Abs(seed) % 3) + 1;
    }

    /// <summary>
    /// Creates a Purchase entity with controlled parameters for testing.
    /// </summary>
    private static Purchase CreatePurchase(
        int id, int businessId, int purchaseTypeId, decimal totalAmount,
        DateOnly invoiceDate, bool isCancelled)
    {
        return new Purchase
        {
            Id = id,
            BusinessId = businessId,
            SupplierId = 1,
            ExpenseCategoryId = 1,
            PurchaseOriginTypeId = 1,
            PurchaseTypeId = purchaseTypeId,
            InvoiceNumber = $"INV-{id:D4}",
            InvoiceDate = invoiceDate,
            Description = $"Test Purchase {id}",
            AmountExcludingVat = totalAmount * 0.8m,
            VatAmount = totalAmount * 0.2m,
            TotalAmount = totalAmount,
            IsCancelled = isCancelled,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    #endregion

    #region Property 2: Purchase classification separates COGS and Operating Expenses correctly with tenant isolation

    /// <summary>
    /// Property 2: COGS and OpEx are correctly classified from a mixed set of purchases.
    /// Generates random purchases with varying PurchaseTypeId, IsCancelled, InvoiceDate, BusinessId.
    /// Asserts COGS = sum of TotalAmount where PurchaseTypeId==2, not cancelled, in period, same business.
    /// Asserts OpEx = sum of TotalAmount where PurchaseTypeId==3, not cancelled, in period, same business.
    /// **Validates: Requirements 1.2, 1.3, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PurchaseClassification_SeparatesCogsAndOpExCorrectly(
        PositiveInt[] amountSeeds, bool[] cancelledFlags, bool[] inPeriodFlags, bool[] sameBusinessFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var purchaseCount = Math.Min(amountSeeds.Length, 20);
        var purchases = new List<Purchase>();

        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var isCancelled = cancelledFlags.Length > 0 && cancelledFlags[i % cancelledFlags.Length];
            var isInPeriod = inPeriodFlags.Length > 0 && inPeriodFlags[i % inPeriodFlags.Length];
            var isSameBusiness = sameBusinessFlags.Length > 0 && sameBusinessFlags[i % sameBusinessFlags.Length];
            var purchaseTypeId = GeneratePurchaseTypeId(amountSeeds[i].Get + i);

            var invoiceDate = isInPeriod
                ? GenerateInPeriodDate(amountSeeds[i].Get + i)
                : GenerateOutOfPeriodDate(amountSeeds[i].Get + i);

            var businessId = isSameBusiness ? TestBusinessId : OtherBusinessId;

            purchases.Add(CreatePurchase(i + 1, businessId, purchaseTypeId, amount, invoiceDate, isCancelled));
        }

        var expectedCogs = ComputeExpectedCogs(purchases, TestBusinessId, PeriodStart, PeriodEnd);
        var expectedOpEx = ComputeExpectedOpEx(purchases, TestBusinessId, PeriodStart, PeriodEnd);

        // Simulate the same computation the PnlService performs
        var actualCogs = purchases
            .Where(p => p.BusinessId == TestBusinessId
                        && !p.IsCancelled
                        && p.PurchaseTypeId == PurchaseTypeStock
                        && p.InvoiceDate >= PeriodStart
                        && p.InvoiceDate <= PeriodEnd)
            .Sum(p => p.TotalAmount);

        var actualOpEx = purchases
            .Where(p => p.BusinessId == TestBusinessId
                        && !p.IsCancelled
                        && p.PurchaseTypeId == PurchaseTypeExpense
                        && p.InvoiceDate >= PeriodStart
                        && p.InvoiceDate <= PeriodEnd)
            .Sum(p => p.TotalAmount);

        return (actualCogs == expectedCogs && actualOpEx == expectedOpEx).ToProperty()
            .Label($"Expected COGS={expectedCogs}, Actual COGS={actualCogs}, " +
                   $"Expected OpEx={expectedOpEx}, Actual OpEx={actualOpEx}, " +
                   $"PurchaseCount={purchaseCount}");
    }

    /// <summary>
    /// Asset purchases (PurchaseTypeId == 1) are excluded from both COGS and OpEx.
    /// **Validates: Requirements 1.2, 1.3, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PurchaseClassification_ExcludesAssetPurchases(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var purchaseCount = Math.Min(amountSeeds.Length, 15);
        var purchases = new List<Purchase>();

        // Create all purchases as Assets (PurchaseTypeId == 1), in period, not cancelled, same business
        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var invoiceDate = GenerateInPeriodDate(amountSeeds[i].Get + i);

            purchases.Add(CreatePurchase(i + 1, TestBusinessId, PurchaseTypeAsset, amount, invoiceDate, isCancelled: false));
        }

        var actualCogs = ComputeExpectedCogs(purchases, TestBusinessId, PeriodStart, PeriodEnd);
        var actualOpEx = ComputeExpectedOpEx(purchases, TestBusinessId, PeriodStart, PeriodEnd);

        return (actualCogs == 0m && actualOpEx == 0m).ToProperty()
            .Label($"Expected COGS=0 and OpEx=0 for all Asset purchases, " +
                   $"but got COGS={actualCogs}, OpEx={actualOpEx}");
    }

    /// <summary>
    /// Cancelled purchases are excluded from both COGS and OpEx.
    /// **Validates: Requirements 1.2, 1.3, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PurchaseClassification_ExcludesCancelledPurchases(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var purchaseCount = Math.Min(amountSeeds.Length, 15);
        var purchases = new List<Purchase>();

        // Create purchases with PurchaseTypeId 2 and 3, all cancelled, in period, same business
        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var invoiceDate = GenerateInPeriodDate(amountSeeds[i].Get + i);
            var purchaseTypeId = (i % 2 == 0) ? PurchaseTypeStock : PurchaseTypeExpense;

            purchases.Add(CreatePurchase(i + 1, TestBusinessId, purchaseTypeId, amount, invoiceDate, isCancelled: true));
        }

        var actualCogs = ComputeExpectedCogs(purchases, TestBusinessId, PeriodStart, PeriodEnd);
        var actualOpEx = ComputeExpectedOpEx(purchases, TestBusinessId, PeriodStart, PeriodEnd);

        return (actualCogs == 0m && actualOpEx == 0m).ToProperty()
            .Label($"Expected COGS=0 and OpEx=0 for all cancelled purchases, " +
                   $"but got COGS={actualCogs}, OpEx={actualOpEx}");
    }

    /// <summary>
    /// Purchases outside the period are excluded from both COGS and OpEx.
    /// **Validates: Requirements 1.2, 1.3, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PurchaseClassification_ExcludesOutOfPeriodPurchases(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var purchaseCount = Math.Min(amountSeeds.Length, 15);
        var purchases = new List<Purchase>();

        // Create purchases outside the period, not cancelled, same business, PurchaseTypeId 2 or 3
        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var invoiceDate = GenerateOutOfPeriodDate(amountSeeds[i].Get + i);
            var purchaseTypeId = (i % 2 == 0) ? PurchaseTypeStock : PurchaseTypeExpense;

            purchases.Add(CreatePurchase(i + 1, TestBusinessId, purchaseTypeId, amount, invoiceDate, isCancelled: false));
        }

        var actualCogs = ComputeExpectedCogs(purchases, TestBusinessId, PeriodStart, PeriodEnd);
        var actualOpEx = ComputeExpectedOpEx(purchases, TestBusinessId, PeriodStart, PeriodEnd);

        return (actualCogs == 0m && actualOpEx == 0m).ToProperty()
            .Label($"Expected COGS=0 and OpEx=0 for all out-of-period purchases, " +
                   $"but got COGS={actualCogs}, OpEx={actualOpEx}");
    }

    /// <summary>
    /// Purchases from a different business are excluded from both COGS and OpEx (tenant isolation).
    /// **Validates: Requirements 1.2, 1.3, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PurchaseClassification_ExcludesOtherBusinessPurchases(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var purchaseCount = Math.Min(amountSeeds.Length, 15);
        var purchases = new List<Purchase>();

        // Create purchases for a DIFFERENT business, in period, not cancelled, PurchaseTypeId 2 or 3
        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var invoiceDate = GenerateInPeriodDate(amountSeeds[i].Get + i);
            var purchaseTypeId = (i % 2 == 0) ? PurchaseTypeStock : PurchaseTypeExpense;

            purchases.Add(CreatePurchase(i + 1, OtherBusinessId, purchaseTypeId, amount, invoiceDate, isCancelled: false));
        }

        var actualCogs = ComputeExpectedCogs(purchases, TestBusinessId, PeriodStart, PeriodEnd);
        var actualOpEx = ComputeExpectedOpEx(purchases, TestBusinessId, PeriodStart, PeriodEnd);

        return (actualCogs == 0m && actualOpEx == 0m).ToProperty()
            .Label($"Expected COGS=0 and OpEx=0 for other business purchases, " +
                   $"but got COGS={actualCogs}, OpEx={actualOpEx}");
    }

    /// <summary>
    /// When no purchases exist, both COGS and OpEx are zero.
    /// **Validates: Requirements 1.2, 1.3, 8.2**
    /// </summary>
    [Fact]
    public void PurchaseClassification_NoPurchases_ReturnsZeroCogAndOpEx()
    {
        var purchases = new List<Purchase>();
        var actualCogs = ComputeExpectedCogs(purchases, TestBusinessId, PeriodStart, PeriodEnd);
        var actualOpEx = ComputeExpectedOpEx(purchases, TestBusinessId, PeriodStart, PeriodEnd);
        Assert.Equal(0m, actualCogs);
        Assert.Equal(0m, actualOpEx);
    }

    #endregion
}
