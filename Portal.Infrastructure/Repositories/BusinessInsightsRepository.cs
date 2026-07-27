using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for aggregating business activity metrics from the Portal database.
/// Used exclusively by the SuperAdmin Business Insights page.
/// Uses IgnoreQueryFilters() to bypass tenant scoping and see all businesses.
/// </summary>
public class BusinessInsightsRepository
{
    private readonly PortalDbContext _context;

    public BusinessInsightsRepository(PortalDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns all non-demo businesses with their plan info and aggregated activity counts.
    /// Bypasses global tenant query filters to provide a platform-wide view.
    /// </summary>
    public async Task<List<BusinessActivityRow>> GetBusinessActivityAsync()
    {
        try
        {
            // 1. Get all non-demo businesses with their plan info
            var businesses = await (
                from business in _context.Businesses
                where !business.IsDemoAccount
                join businessPlan in _context.BusinessPlans on business.Id equals businessPlan.BusinessId into bpGroup
                from bp in bpGroup.DefaultIfEmpty()
                join plan in _context.Plans on bp.PlanId equals plan.Id into planGroup
                from p in planGroup.DefaultIfEmpty()
                select new
                {
                    business.Id,
                    business.Name,
                    PlanName = p != null ? p.Name : "No Plan",
                    Status = bp != null ? bp.Status : "unknown",
                    TrialEndsAtUtc = bp != null ? bp.TrialEndsAtUtc : (DateTime?)null
                }
            ).ToListAsync();

            var businessIds = businesses.Select(b => b.Id).ToList();

            // 2. Aggregate counts bypassing tenant query filters
            var quotationCounts = await _context.Quotations
                .IgnoreQueryFilters()
                .Where(q => businessIds.Contains(q.BusinessId))
                .GroupBy(q => q.BusinessId)
                .Select(g => new { BusinessId = g.Key, Count = g.Count(), LastDate = g.Max(q => (DateTime?)q.CreatedAtUtc) })
                .ToListAsync();

            var invoiceData = await _context.Invoices
                .IgnoreQueryFilters()
                .Where(i => businessIds.Contains(i.BusinessId))
                .GroupBy(i => i.BusinessId)
                .Select(g => new { BusinessId = g.Key, Count = g.Count(), Revenue = g.Sum(i => i.TotalAmount), LastDate = g.Max(i => (DateTime?)i.CreatedAtUtc) })
                .ToListAsync();

            var purchaseCounts = await _context.Purchases
                .IgnoreQueryFilters()
                .Where(pu => businessIds.Contains(pu.BusinessId))
                .GroupBy(pu => pu.BusinessId)
                .Select(g => new { BusinessId = g.Key, Count = g.Count(), LastDate = g.Max(pu => (DateTime?)pu.CreatedAtUtc) })
                .ToListAsync();

            // 3. Build lookup dictionaries
            var quotationLookup = quotationCounts.ToDictionary(x => x.BusinessId);
            var invoiceLookup = invoiceData.ToDictionary(x => x.BusinessId);
            var purchaseLookup = purchaseCounts.ToDictionary(x => x.BusinessId);

            // 4. Assemble results
            var results = businesses.Select(b => new BusinessActivityRow
            {
                BusinessId = b.Id,
                BusinessName = b.Name,
                PlanName = b.PlanName,
                Status = b.Status,
                TrialEndsAtUtc = b.TrialEndsAtUtc,
                QuotationCount = quotationLookup.TryGetValue(b.Id, out var qc) ? qc.Count : 0,
                InvoiceCount = invoiceLookup.TryGetValue(b.Id, out var ic) ? ic.Count : 0,
                PurchaseCount = purchaseLookup.TryGetValue(b.Id, out var pc) ? pc.Count : 0,
                RevenueTotal = invoiceLookup.TryGetValue(b.Id, out var ir) ? ir.Revenue : 0m,
                LastQuotationDate = quotationLookup.TryGetValue(b.Id, out var ql) ? ql.LastDate : null,
                LastInvoiceDate = invoiceLookup.TryGetValue(b.Id, out var il) ? il.LastDate : null,
                LastPurchaseDate = purchaseLookup.TryGetValue(b.Id, out var pl) ? pl.LastDate : null
            }).ToList();

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}

/// <summary>
/// Raw row returned from the aggregation query, before cross-database enrichment.
/// </summary>
public class BusinessActivityRow
{
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = null!;
    public string PlanName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? TrialEndsAtUtc { get; set; }
    public int QuotationCount { get; set; }
    public int InvoiceCount { get; set; }
    public int PurchaseCount { get; set; }
    public decimal RevenueTotal { get; set; }
    public DateTime? LastQuotationDate { get; set; }
    public DateTime? LastInvoiceDate { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
}
