using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Computes all analytics metrics for the Supplier Dashboard page.
/// Queries are scoped to the current tenant via EF Core global query filters on BusinessId.
/// </summary>
public class SupplierDashboardService : ISupplierDashboardService
{
    private readonly PortalDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;
    private const int PageSize = 10;

    public SupplierDashboardService(
        PortalDbContext dbContext,
        ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
    }

    /// <inheritdoc />
    public async Task<SupplierDashboardViewModel> GetDashboardAsync(
        int supplierId,
        int? periodId,
        int page,
        string? description = null,
        int? categoryId = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Validate periodId belongs to the current business; ignore it if not
        int? validatedPeriodId = null;
        if (periodId.HasValue)
        {
            var periodExists = await _dbContext.VatSubmissionPeriods
                .AnyAsync(p => p.Id == periodId.Value);

            if (periodExists)
                validatedPeriodId = periodId.Value;
        }

        // Fetch supplier info (global query filter ensures it belongs to current business)
        var supplier = await _dbContext.Suppliers
            .Where(s => s.Id == supplierId)
            .Select(s => new { s.Id, s.Name, s.IsActive, s.CreatedAtUtc })
            .FirstOrDefaultAsync();

        // Fetch currency symbol from BusinessProfile (no global query filter on BusinessProfile)
        var currencySymbol = await _dbContext.BusinessProfiles
            .Where(bp => bp.BusinessId == businessId)
            .Select(bp => bp.CurrencySymbol)
            .FirstOrDefaultAsync() ?? "€";

        // Build base query: non-cancelled purchases for this supplier, scoped by period if provided
        // IMPORTANT: baseQuery is used for KPIs and charts — purchase filters do NOT apply here
        var baseQuery = _dbContext.Purchases
            .Where(p => p.SupplierId == supplierId && !p.IsCancelled);

        if (validatedPeriodId.HasValue)
            baseQuery = baseQuery.Where(p => p.VatSubmissionPeriodId == validatedPeriodId.Value);

        // --- Purchase filter validation ---

        // Validate categoryId: must reference an active ExpenseCategory for the current business
        // (global query filter already scopes ExpenseCategories to current business)
        int? validatedCategoryId = null;
        if (categoryId.HasValue)
        {
            var categoryExists = await _dbContext.ExpenseCategories
                .AnyAsync(c => c.Id == categoryId.Value && c.IsActive);

            if (categoryExists)
                validatedCategoryId = categoryId.Value;
        }

        // Validate date range: if both provided and dateFrom > dateTo, ignore both
        DateOnly? validatedDateFrom = dateFrom;
        DateOnly? validatedDateTo = dateTo;
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
        {
            validatedDateFrom = null;
            validatedDateTo = null;
        }

        // --- Build purchaseQuery from baseQuery with filter predicates ---
        // Only the purchases table uses this filtered query
        var purchaseQuery = baseQuery;

        if (!string.IsNullOrWhiteSpace(description))
            purchaseQuery = purchaseQuery.Where(p => p.Description != null && p.Description.Contains(description));

        if (validatedCategoryId.HasValue)
            purchaseQuery = purchaseQuery.Where(p => p.ExpenseCategoryId == validatedCategoryId.Value);

        if (validatedDateFrom.HasValue)
            purchaseQuery = purchaseQuery.Where(p => p.InvoiceDate >= validatedDateFrom.Value);

        if (validatedDateTo.HasValue)
            purchaseQuery = purchaseQuery.Where(p => p.InvoiceDate <= validatedDateTo.Value);

        // Compute KPIs and charts from baseQuery (unaffected by purchase filters)
        var kpis = await ComputeKpisAsync(baseQuery);
        var spendShare = await ComputeSpendShareAsync(supplierId, validatedPeriodId);
        var monthlySpend = await ComputeMonthlySpendAsync(baseQuery);
        var periodSpend = await ComputePeriodSpendAsync(supplierId, validatedPeriodId);

        // Purchases table uses the filtered query
        var (purchases, currentPage, totalPages, totalRecords) = await GetPurchasesPageAsync(purchaseQuery, page);
        var periodOptions = await GetPeriodOptionsAsync();

        // Fetch active expense categories for the filter dropdown (sorted alphabetically)
        var expenseCategories = await _dbContext.ExpenseCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ExpenseCategoryOption
            {
                Id = c.Id,
                Name = c.Name
            })
            .ToListAsync();

        return new SupplierDashboardViewModel
        {
            SupplierId = supplierId,
            SupplierName = supplier?.Name ?? string.Empty,
            CollaborationSince = supplier?.CreatedAtUtc ?? DateTime.MinValue,
            IsActive = supplier?.IsActive ?? false,
            CurrencySymbol = currencySymbol,
            SelectedPeriodId = validatedPeriodId,
            Periods = periodOptions,
            TotalSpend = kpis.TotalSpend,
            TotalPurchases = kpis.TotalPurchases,
            AverageMonthlySpend = kpis.AverageMonthlySpend,
            SpendShareData = spendShare,
            MonthlySpendData = monthlySpend,
            PeriodSpendData = periodSpend,
            Purchases = purchases,
            CurrentPage = currentPage,
            TotalPages = totalPages,
            TotalRecords = totalRecords,
            FilterDescription = description,
            FilterCategoryId = validatedCategoryId,
            FilterDateFrom = validatedDateFrom,
            FilterDateTo = validatedDateTo,
            ExpenseCategories = expenseCategories
        };
    }

    /// <summary>
    /// Computes Total Spend, Total Purchases, and Average Monthly Spend from the base query.
    /// Returns zeros when no purchases exist.
    /// </summary>
    private static async Task<(decimal TotalSpend, int TotalPurchases, decimal AverageMonthlySpend)> ComputeKpisAsync(
        IQueryable<Entities.Purchase> query)
    {
        // Pull the minimal projection needed for KPI computation
        var data = await query
            .Select(p => new { p.AmountExcludingVat, p.InvoiceDate })
            .ToListAsync();

        if (data.Count == 0)
            return (0m, 0, 0m);

        var totalSpend = data.Sum(p => p.AmountExcludingVat);
        var totalPurchases = data.Count;

        // Distinct calendar months (year + month) containing at least one purchase
        var distinctMonths = data
            .Select(p => new { p.InvoiceDate.Year, p.InvoiceDate.Month })
            .Distinct()
            .Count();

        var averageMonthlySpend = distinctMonths > 0
            ? totalSpend / distinctMonths
            : 0m;

        return (totalSpend, totalPurchases, averageMonthlySpend);
    }

    /// <summary>
    /// Ranks all suppliers by spend in the selected period, returns:
    /// - The current supplier's slice
    /// - Top 5 other suppliers by descending spend
    /// - An "Others" aggregate slice if more than 5 other suppliers remain
    /// </summary>
    private async Task<List<SpendShareSlice>> ComputeSpendShareAsync(int supplierId, int? periodId)
    {
        var allSupplierSpend = await _dbContext.Purchases
            .Where(p => !p.IsCancelled)
            .Where(p => !periodId.HasValue || p.VatSubmissionPeriodId == periodId.Value)
            .GroupBy(p => new { p.SupplierId, p.Supplier.Name })
            .Select(g => new
            {
                g.Key.SupplierId,
                g.Key.Name,
                Total = g.Sum(p => p.AmountExcludingVat)
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync();

        var result = new List<SpendShareSlice>();

        // Ensure the current supplier always appears, even with zero spend
        var currentSupplierEntry = allSupplierSpend.FirstOrDefault(x => x.SupplierId == supplierId);
        var currentSupplierName = currentSupplierEntry?.Name
            ?? await _dbContext.Suppliers
                .Where(s => s.Id == supplierId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync()
            ?? string.Empty;

        result.Add(new SpendShareSlice
        {
            SupplierName = currentSupplierName,
            Amount = currentSupplierEntry?.Total ?? 0m,
            IsCurrentSupplier = true
        });

        // Other suppliers ranked by spend
        var others = allSupplierSpend
            .Where(x => x.SupplierId != supplierId)
            .ToList();

        var top5 = others.Take(5).ToList();
        var remaining = others.Skip(5).ToList();

        foreach (var entry in top5)
        {
            result.Add(new SpendShareSlice
            {
                SupplierName = entry.Name,
                Amount = entry.Total,
                IsCurrentSupplier = false
            });
        }

        if (remaining.Count > 0)
        {
            result.Add(new SpendShareSlice
            {
                SupplierName = "Others",
                Amount = remaining.Sum(x => x.Total),
                IsCurrentSupplier = false
            });
        }

        return result;
    }

    /// <summary>
    /// Groups the base query by calendar month (year + month) and returns one bar per month.
    /// </summary>
    private static async Task<List<MonthlySpendBar>> ComputeMonthlySpendAsync(
        IQueryable<Entities.Purchase> query)
    {
        var grouped = await query
            .GroupBy(p => new { p.InvoiceDate.Year, p.InvoiceDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Amount = g.Sum(p => p.AmountExcludingVat)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();

        return grouped.Select(x => new MonthlySpendBar
        {
            MonthLabel = new DateTime(x.Year, x.Month, 1).ToString("MMM"),
            Year = x.Year,
            Amount = x.Amount
        }).ToList();
    }

    /// <summary>
    /// Groups all non-cancelled purchases for the supplier by VatSubmissionPeriodId,
    /// then joins with all business periods (including zero-spend periods).
    /// Marks the selected period as IsSelected.
    /// </summary>
    private async Task<List<PeriodSpendBar>> ComputePeriodSpendAsync(int supplierId, int? selectedPeriodId)
    {
        // Sum spend per period for this supplier
        var spendByPeriod = await _dbContext.Purchases
            .Where(p => p.SupplierId == supplierId && !p.IsCancelled && p.VatSubmissionPeriodId != null)
            .GroupBy(p => p.VatSubmissionPeriodId!.Value)
            .Select(g => new { PeriodId = g.Key, Amount = g.Sum(p => p.AmountExcludingVat) })
            .ToListAsync();

        // All business periods ordered by start date
        var allPeriods = await _dbContext.VatSubmissionPeriods
            .OrderBy(p => p.PeriodStartDate)
            .Select(p => new { p.Id, p.PeriodLabel })
            .ToListAsync();

        var spendLookup = spendByPeriod.ToDictionary(x => x.PeriodId, x => x.Amount);

        return allPeriods.Select(p => new PeriodSpendBar
        {
            PeriodId = p.Id,
            PeriodLabel = p.PeriodLabel,
            Amount = spendLookup.TryGetValue(p.Id, out var amount) ? amount : 0m,
            IsSelected = selectedPeriodId.HasValue && p.Id == selectedPeriodId.Value
        }).ToList();
    }

    /// <summary>
    /// Returns a paginated, sorted (by InvoiceDate ascending) page of purchases.
    /// Clamps the page number to the valid range.
    /// </summary>
    private static async Task<(List<PurchaseTableRow> Purchases, int CurrentPage, int TotalPages, int TotalRecords)>
        GetPurchasesPageAsync(IQueryable<Entities.Purchase> query, int page)
    {
        var totalRecords = await query.CountAsync();
        var totalPages = totalRecords == 0 ? 1 : (int)Math.Ceiling(totalRecords / (double)PageSize);

        // Clamp page to valid range
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        var purchases = await query
            .OrderBy(p => p.InvoiceDate)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(p => new PurchaseTableRow
            {
                InvoiceDate = p.InvoiceDate,
                Description = p.Description ?? string.Empty,
                Category = p.ExpenseCategory.Name,
                AmountExcludingVat = p.AmountExcludingVat,
                VatAmount = p.VatAmount,
                TotalAmount = p.TotalAmount
            })
            .ToListAsync();

        return (purchases, page, totalPages, totalRecords);
    }

    /// <summary>
    /// Returns all VAT submission periods for the current business, ordered by PeriodStartDate ascending.
    /// </summary>
    private async Task<List<VatPeriodOption>> GetPeriodOptionsAsync()
    {
        return await _dbContext.VatSubmissionPeriods
            .OrderBy(p => p.PeriodStartDate)
            .Select(p => new VatPeriodOption
            {
                Id = p.Id,
                Label = p.PeriodLabel
            })
            .ToListAsync();
    }
}
