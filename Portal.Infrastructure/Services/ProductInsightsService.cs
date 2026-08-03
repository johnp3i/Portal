using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models.ProductInsights;

namespace Portal.Infrastructure.Services;

public class ProductInsightsService : IProductInsightsService
{
    private readonly PortalDbContext _dbContext;

    public ProductInsightsService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductKpiDto> GetSalesKpisAsync(string productCode, int businessId, decimal costPrice)
    {
        var query = _dbContext.InvoiceLines
            .Include(l => l.Invoice)
            .Where(l => l.Invoice != null
                && l.Invoice.BusinessId == businessId
                && l.Invoice.InvoiceStatusTypeId == 2
                && !l.Invoice.IsDeleted
                && l.ProductCode == productCode);

        var lines = await query.Select(l => new { l.LineTotal, l.Quantity, l.Invoice!.InvoiceDate }).ToListAsync();

        if (lines.Count == 0)
            return new ProductKpiDto();

        var totalRevenue = lines.Sum(l => l.LineTotal);
        var totalUnits = lines.Sum(l => l.Quantity);
        var invoiceCount = lines.Select(l => l.InvoiceDate).Distinct().Count();
        var lastSold = lines.Max(l => l.InvoiceDate);
        var avgPrice = totalUnits > 0 ? totalRevenue / totalUnits : 0;
        var grossMargin = totalRevenue - (costPrice * totalUnits);
        var marginPct = totalRevenue > 0 ? (grossMargin / totalRevenue) * 100 : 0;

        return new ProductKpiDto
        {
            TotalRevenue = totalRevenue,
            TotalUnits = totalUnits,
            AvgSellingPrice = Math.Round(avgPrice, 2),
            GrossMargin = grossMargin,
            MarginPercentage = Math.Round(marginPct, 1),
            LastSoldDate = lastSold.ToDateTime(TimeOnly.MinValue),
            InvoiceCount = invoiceCount
        };
    }

    public async Task<List<ProductCustomerDto>> GetTopCustomersAsync(string productCode, int businessId, int top = 5)
    {
        var data = await _dbContext.InvoiceLines
            .Include(l => l.Invoice).ThenInclude(i => i!.Customer)
            .Where(l => l.Invoice != null
                && l.Invoice.BusinessId == businessId
                && l.Invoice.InvoiceStatusTypeId == 2
                && !l.Invoice.IsDeleted
                && l.ProductCode == productCode)
            .Select(l => new { CustomerName = l.Invoice!.Customer!.Name, l.Quantity, l.LineTotal, l.Invoice.InvoiceDate })
            .ToListAsync();

        var results = data
            .GroupBy(l => l.CustomerName)
            .Select(g => new ProductCustomerDto
            {
                CustomerName = g.Key,
                Units = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => l.LineTotal),
                LastPurchase = g.Max(l => l.InvoiceDate.ToDateTime(TimeOnly.MinValue))
            })
            .OrderByDescending(c => c.Revenue)
            .Take(top)
            .ToList();

        return results;
    }

    public async Task<ProductCustomerSummaryDto> GetCustomerSummaryAsync(string productCode, int businessId)
    {
        var customerIds = await _dbContext.InvoiceLines
            .Include(l => l.Invoice)
            .Where(l => l.Invoice != null
                && l.Invoice.BusinessId == businessId
                && l.Invoice.InvoiceStatusTypeId == 2
                && !l.Invoice.IsDeleted
                && l.ProductCode == productCode)
            .Select(l => l.Invoice!.CustomerId)
            .ToListAsync();

        var grouped = customerIds.GroupBy(id => id).ToList();
        var uniqueCount = grouped.Count;
        var repeatCount = grouped.Count(g => g.Count() > 1);
        var repeatRate = uniqueCount > 0 ? Math.Round((decimal)repeatCount / uniqueCount * 100, 0) : 0;

        return new ProductCustomerSummaryDto
        {
            UniqueCustomerCount = uniqueCount,
            RepeatPurchaseRate = repeatRate
        };
    }

    public async Task<List<MonthlyProductRevenueDto>> GetMonthlyTrendAsync(string productCode, int businessId, int months = 12)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-months));

        var data = await _dbContext.InvoiceLines
            .Include(l => l.Invoice)
            .Where(l => l.Invoice != null
                && l.Invoice.BusinessId == businessId
                && l.Invoice.InvoiceStatusTypeId == 2
                && !l.Invoice.IsDeleted
                && l.ProductCode == productCode
                && l.Invoice.InvoiceDate >= cutoff)
            .GroupBy(l => new { l.Invoice!.InvoiceDate.Year, l.Invoice.InvoiceDate.Month })
            .Select(g => new MonthlyProductRevenueDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM"),
                Revenue = g.Sum(l => l.LineTotal)
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToListAsync();

        // Fill in missing months with zero
        var result = new List<MonthlyProductRevenueDto>();
        var start = DateTime.UtcNow.AddMonths(-months + 1);
        for (int i = 0; i < months; i++)
        {
            var d = start.AddMonths(i);
            var existing = data.FirstOrDefault(m => m.Year == d.Year && m.Month == d.Month);
            result.Add(existing ?? new MonthlyProductRevenueDto
            {
                Year = d.Year,
                Month = d.Month,
                Label = d.ToString("MMM"),
                Revenue = 0
            });
        }

        return result;
    }

    public async Task<ProductForecastDto> GetForecastAsync(string productCode, int businessId, decimal sellingPrice)
    {
        var sixMonthsAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6));

        var totals = await _dbContext.InvoiceLines
            .Include(l => l.Invoice)
            .Where(l => l.Invoice != null
                && l.Invoice.BusinessId == businessId
                && l.Invoice.InvoiceStatusTypeId == 2
                && !l.Invoice.IsDeleted
                && l.ProductCode == productCode
                && l.Invoice.InvoiceDate >= sixMonthsAgo)
            .GroupBy(l => 1)
            .Select(g => new { Revenue = g.Sum(l => l.LineTotal), Units = g.Sum(l => l.Quantity) })
            .FirstOrDefaultAsync();

        if (totals == null)
            return new ProductForecastDto();

        var avgUnits = totals.Units / 6m;
        var avgRevenue = totals.Revenue / 6m;

        return new ProductForecastDto
        {
            AvgMonthlyUnits = Math.Round(avgUnits, 0),
            AvgMonthlyRevenue = Math.Round(avgRevenue, 2),
            Forecast30Units = Math.Round(avgUnits, 0),
            Forecast30Revenue = Math.Round(avgRevenue, 2),
            Forecast60Units = Math.Round(avgUnits * 2, 0),
            Forecast60Revenue = Math.Round(avgRevenue * 2, 2),
            Forecast90Units = Math.Round(avgUnits * 3, 0),
            Forecast90Revenue = Math.Round(avgRevenue * 3, 2)
        };
    }

    public async Task<ProductPipelineDto?> GetPipelineActivityAsync(int productId, int businessId)
    {
        // Check if any sales products link to this catalog product
        var linkedSalesProductIds = await _dbContext.Set<Portal.Infrastructure.Entities.Sales.SalesProduct>()
            .IgnoreQueryFilters()
            .Where(sp => sp.BusinessId == businessId && sp.ProductId == productId && sp.IsActive)
            .Select(sp => sp.Id)
            .ToListAsync();

        if (!linkedSalesProductIds.Any())
            return null;

        // Query active leads that reference these sales products (via single ProductId FK)
        var activeLeads = await _dbContext.Set<Portal.Infrastructure.Entities.Sales.LeadRequest>()
            .IgnoreQueryFilters()
            .Include(l => l.LeadStatusType)
            .Include(l => l.Contact)
            .Where(l => l.BusinessId == businessId
                && l.IsActive
                && !l.IsCancelled
                && l.ProductId.HasValue
                && linkedSalesProductIds.Contains(l.ProductId.Value)
                && l.LeadStatusType != null
                && l.LeadStatusType.Name != "Won"
                && l.LeadStatusType.Name != "Lost"
                && l.LeadStatusType.Name != "Inactive")
            .Select(l => new PipelineLeadDto
            {
                LeadId = l.Id,
                Title = l.Contact != null ? (l.Contact.FirstName + " " + l.Contact.LastName).Trim() : "Lead #" + l.Id,
                Stage = l.LeadStatusType!.Name,
                EstimatedValue = 0,
                AssignedTo = null
            })
            .Take(5)
            .ToListAsync();

        // Calculate conversion rate
        var totalLeadsWithProduct = await _dbContext.Set<Portal.Infrastructure.Entities.Sales.LeadRequest>()
            .IgnoreQueryFilters()
            .Where(l => l.BusinessId == businessId
                && l.ProductId.HasValue
                && linkedSalesProductIds.Contains(l.ProductId.Value))
            .CountAsync();

        var wonLeads = await _dbContext.Set<Portal.Infrastructure.Entities.Sales.LeadRequest>()
            .IgnoreQueryFilters()
            .Include(l => l.LeadStatusType)
            .Where(l => l.BusinessId == businessId
                && l.ProductId.HasValue
                && linkedSalesProductIds.Contains(l.ProductId.Value)
                && l.LeadStatusType != null && l.LeadStatusType.Name == "Won")
            .CountAsync();

        var conversionRate = totalLeadsWithProduct > 0 ? Math.Round((decimal)wonLeads / totalLeadsWithProduct * 100, 0) : 0;

        return new ProductPipelineDto
        {
            ActiveLeadCount = activeLeads.Count,
            EstimatedValue = 0,
            ConversionRate = conversionRate,
            Leads = activeLeads
        };
    }
}
