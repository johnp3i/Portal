using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Models.Stripe;
using Portal.Web.Services;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 12: Billing invoice ordering

/// <summary>
/// Property-based tests for billing invoice ordering.
/// For any set of billing invoices belonging to a Business, the Billing page SHALL return them
/// ordered by PaidAtUtc descending (most recent first), and pagination SHALL preserve this ordering across pages.
/// **Validates: Requirements 9.3**
/// </summary>
public class BillingInvoiceOrderingPropertyTests
{
    /// <summary>
    /// Creates a BillingService with mocked dependencies, configured to return the given invoices
    /// from GetByBusinessIdPagedAsync in the specified order.
    /// </summary>
    private static BillingService CreateBillingServiceWithInvoices(
        int businessId,
        List<BillingInvoice> invoicesInOrder,
        int page,
        int pageSize,
        int totalCount)
    {
        var subscriptionRepoMock = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
        var billingInvoiceRepoMock = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        var billingPaymentRepoMock = new Mock<BillingPaymentRepository>(MockBehavior.Loose, new object[] { null! });
        var planRepoMock = new Mock<IPlanRepository>();
        var businessServiceMock = new Mock<IBusinessService>();
        var viewRenderServiceMock = new Mock<IViewRenderService>();
        var loggerMock = new Mock<ILogger<BillingService>>();

        billingInvoiceRepoMock
            .Setup(r => r.GetByBusinessIdPagedAsync(businessId, page, pageSize))
            .ReturnsAsync((invoicesInOrder, totalCount));

        return new BillingService(
            subscriptionRepoMock.Object,
            billingInvoiceRepoMock.Object,
            billingPaymentRepoMock.Object,
            planRepoMock.Object,
            businessServiceMock.Object,
            viewRenderServiceMock.Object,
            loggerMock.Object);
    }

    #region Property 12a: Invoices returned are ordered by PaidAtUtc descending

    /// <summary>
    /// Property 12a: For any set of billing invoices with distinct PaidAtUtc values,
    /// GetInvoicesAsync SHALL return them ordered by PaidAtUtc descending (most recent first).
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetInvoicesAsync_ReturnsInvoicesOrderedByPaidAtUtcDescending(
        PositiveInt businessIdSeed,
        PositiveInt invoiceCountSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var invoiceCount = (invoiceCountSeed.Get % 10) + 2; // At least 2 invoices to test ordering

        // Generate invoices with distinct PaidAtUtc values in random order
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var invoices = new List<BillingInvoice>();

        for (int i = 0; i < invoiceCount; i++)
        {
            invoices.Add(new BillingInvoice
            {
                Id = i + 1,
                BusinessId = businessId,
                StripeInvoiceId = $"inv_{i + 1}",
                AmountEur = 29.99m + i,
                PeriodStart = baseDate.AddMonths(i),
                PeriodEnd = baseDate.AddMonths(i + 1),
                Status = "paid",
                PaidAtUtc = baseDate.AddDays(i * 30 + (i % 7)), // Distinct dates
                CreatedAtUtc = baseDate.AddDays(i * 30)
            });
        }

        // Sort descending by PaidAtUtc (simulating what the repository SQL does)
        var sortedInvoices = invoices.OrderByDescending(inv => inv.PaidAtUtc).ToList();

        // Create service with invoices already in correct order (as the repository would return them)
        var service = CreateBillingServiceWithInvoices(businessId, sortedInvoices, 1, invoiceCount, invoiceCount);

        // Act
        var result = service.GetInvoicesAsync(businessId, 1, invoiceCount).GetAwaiter().GetResult();

        // Verify: items are in descending PaidAtUtc order
        var isDescending = true;
        for (int i = 0; i < result.Items.Count - 1; i++)
        {
            if (result.Items[i].PaidAtUtc < result.Items[i + 1].PaidAtUtc)
            {
                isDescending = false;
                break;
            }
        }

        return isDescending.ToProperty()
            .Label($"businessId={businessId}, invoiceCount={invoiceCount}: " +
                   $"isDescending={isDescending}");
    }

    #endregion

    #region Property 12b: Pagination preserves descending order across pages

    /// <summary>
    /// Property 12b: For any set of billing invoices spanning multiple pages,
    /// the last item on page N has a PaidAtUtc >= the first item on page N+1,
    /// preserving descending order across page boundaries.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetInvoicesAsync_PaginationPreservesDescendingOrder(
        PositiveInt businessIdSeed,
        PositiveInt totalInvoicesSeed,
        PositiveInt pageSizeSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var totalInvoices = (totalInvoicesSeed.Get % 15) + 4; // At least 4 invoices
        var pageSize = (pageSizeSeed.Get % 3) + 2; // Page size 2-4 to ensure multiple pages

        // Generate invoices with distinct PaidAtUtc values
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var allInvoices = new List<BillingInvoice>();

        for (int i = 0; i < totalInvoices; i++)
        {
            allInvoices.Add(new BillingInvoice
            {
                Id = i + 1,
                BusinessId = businessId,
                StripeInvoiceId = $"inv_{i + 1}",
                AmountEur = 19.99m + i,
                PeriodStart = baseDate.AddMonths(i),
                PeriodEnd = baseDate.AddMonths(i + 1),
                Status = "paid",
                PaidAtUtc = baseDate.AddDays(i * 31), // Distinct dates
                CreatedAtUtc = baseDate.AddDays(i * 31)
            });
        }

        // Sort all invoices descending by PaidAtUtc (as the repository would)
        var sortedAll = allInvoices.OrderByDescending(inv => inv.PaidAtUtc).ToList();

        // Simulate pagination: page 1 and page 2
        var page1Items = sortedAll.Take(pageSize).ToList();
        var page2Items = sortedAll.Skip(pageSize).Take(pageSize).ToList();

        if (page2Items.Count == 0)
        {
            // Not enough items for a second page, property trivially holds
            return true.ToProperty().Label("Only one page of results, ordering trivially holds");
        }

        // Create services for each page
        var servicePage1 = CreateBillingServiceWithInvoices(businessId, page1Items, 1, pageSize, totalInvoices);
        var servicePage2 = CreateBillingServiceWithInvoices(businessId, page2Items, 2, pageSize, totalInvoices);

        // Act
        var resultPage1 = servicePage1.GetInvoicesAsync(businessId, 1, pageSize).GetAwaiter().GetResult();
        var resultPage2 = servicePage2.GetInvoicesAsync(businessId, 2, pageSize).GetAwaiter().GetResult();

        // Verify: last item on page 1 has PaidAtUtc >= first item on page 2
        var lastOnPage1 = resultPage1.Items.Last();
        var firstOnPage2 = resultPage2.Items.First();

        var crossPageOrderCorrect = lastOnPage1.PaidAtUtc >= firstOnPage2.PaidAtUtc;

        // Verify: each page is internally ordered descending
        var page1Descending = IsDescendingOrder(resultPage1.Items);
        var page2Descending = IsDescendingOrder(resultPage2.Items);

        var allCorrect = crossPageOrderCorrect && page1Descending && page2Descending;

        return allCorrect.ToProperty()
            .Label($"businessId={businessId}, totalInvoices={totalInvoices}, pageSize={pageSize}: " +
                   $"crossPageOrderCorrect={crossPageOrderCorrect}, page1Descending={page1Descending}, " +
                   $"page2Descending={page2Descending}");
    }

    #endregion

    #region Property 12c: Empty invoice set returns empty result

    /// <summary>
    /// Property 12c: For any business with no invoices, GetInvoicesAsync SHALL return
    /// an empty list with zero total count, maintaining the ordering contract vacuously.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetInvoicesAsync_EmptyInvoiceSet_ReturnsEmptyOrderedResult(
        PositiveInt businessIdSeed,
        PositiveInt pageSeed,
        PositiveInt pageSizeSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var page = (pageSeed.Get % 5) + 1;
        var pageSize = (pageSizeSeed.Get % 20) + 5;

        // Create service with empty invoice list
        var service = CreateBillingServiceWithInvoices(businessId, new List<BillingInvoice>(), page, pageSize, 0);

        // Act
        var result = service.GetInvoicesAsync(businessId, page, pageSize).GetAwaiter().GetResult();

        // Verify: empty result with correct metadata
        var isEmpty = result.Items.Count == 0;
        var totalCountIsZero = result.TotalCount == 0;

        return (isEmpty && totalCountIsZero).ToProperty()
            .Label($"businessId={businessId}, page={page}, pageSize={pageSize}: " +
                   $"isEmpty={isEmpty}, totalCountIsZero={totalCountIsZero}");
    }

    #endregion

    #region Property 12d: Single invoice maintains ordering contract

    /// <summary>
    /// Property 12d: For any business with exactly one invoice, GetInvoicesAsync SHALL return
    /// a single-item list that trivially satisfies the descending order property.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetInvoicesAsync_SingleInvoice_MaintainsOrderingContract(
        PositiveInt businessIdSeed,
        PositiveInt dayOffsetSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var dayOffset = (dayOffsetSeed.Get % 365) + 1;

        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var invoice = new BillingInvoice
        {
            Id = 1,
            BusinessId = businessId,
            StripeInvoiceId = "inv_single",
            AmountEur = 49.99m,
            PeriodStart = baseDate,
            PeriodEnd = baseDate.AddMonths(1),
            Status = "paid",
            PaidAtUtc = baseDate.AddDays(dayOffset),
            CreatedAtUtc = baseDate
        };

        var service = CreateBillingServiceWithInvoices(businessId, new List<BillingInvoice> { invoice }, 1, 10, 1);

        // Act
        var result = service.GetInvoicesAsync(businessId, 1, 10).GetAwaiter().GetResult();

        // Verify: single item returned, ordering trivially holds
        var hasSingleItem = result.Items.Count == 1;
        var correctPaidAtUtc = result.Items[0].PaidAtUtc == invoice.PaidAtUtc;
        var isDescending = IsDescendingOrder(result.Items); // Trivially true for single item

        return (hasSingleItem && correctPaidAtUtc && isDescending).ToProperty()
            .Label($"businessId={businessId}, paidAtUtc={invoice.PaidAtUtc}: " +
                   $"hasSingleItem={hasSingleItem}, correctPaidAtUtc={correctPaidAtUtc}, isDescending={isDescending}");
    }

    #endregion

    #region Helpers

    private static bool IsDescendingOrder(List<BillingInvoiceModel> items)
    {
        for (int i = 0; i < items.Count - 1; i++)
        {
            if (items[i].PaidAtUtc < items[i + 1].PaidAtUtc)
                return false;
        }
        return true;
    }

    #endregion
}
