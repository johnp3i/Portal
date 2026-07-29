using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Implements cross-module global search with parallel per-entity queries.
/// Each entity query is fault-isolated — a failure in one doesn't affect others.
/// </summary>
public class GlobalSearchService : IGlobalSearchService
{
    private readonly PortalDbContext _context;

    public GlobalSearchService(PortalDbContext context)
    {
        _context = context;
    }

    public async Task<GlobalSearchResultDto> SearchAsync(string query, int businessId, HashSet<string> permittedModules)
    {
        var likePattern = $"%{query}%";
        var tasks = new List<Task<SearchResultGroup?>>();

        if (permittedModules.Contains(PortalModules.Invoice))
            tasks.Add(SearchInvoicesAsync(likePattern, businessId));

        if (permittedModules.Contains(PortalModules.Customer))
            tasks.Add(SearchCustomersAsync(likePattern, businessId));

        if (permittedModules.Contains(PortalModules.Purchase))
        {
            tasks.Add(SearchPurchasesAsync(likePattern, businessId));
            tasks.Add(SearchSuppliersAsync(likePattern, businessId));
        }

        if (permittedModules.Contains(PortalModules.Quotation))
            tasks.Add(SearchQuotationsAsync(likePattern, businessId));

        if (permittedModules.Contains(PortalModules.Products))
            tasks.Add(SearchProductsAsync(likePattern, businessId));

        var results = await Task.WhenAll(tasks);

        var dto = new GlobalSearchResultDto();
        foreach (var group in results)
        {
            if (group != null && group.Items.Count > 0)
                dto.Groups.Add(group);
        }

        return dto;
    }

    private async Task<SearchResultGroup?> SearchInvoicesAsync(string likePattern, int businessId)
    {
        try
        {
            var items = await _context.Invoices
                .Where(i => i.BusinessId == businessId && !i.IsDeleted)
                .Where(i => EF.Functions.Like(i.InvoiceNumber, likePattern)
                         || EF.Functions.Like(i.Customer.Name, likePattern))
                .OrderByDescending(i => i.CreatedAtUtc)
                .Take(5)
                .Select(i => new SearchResultItem
                {
                    Id = i.Id,
                    Primary = i.InvoiceNumber,
                    Secondary = i.Customer.Name,
                    Url = "/Invoice/Detail/" + i.Id
                })
                .ToListAsync();

            return new SearchResultGroup { Type = "invoice", Label = "Invoices", Items = items };
        }
        catch (Exception ex) { return null; }
    }

    private async Task<SearchResultGroup?> SearchCustomersAsync(string likePattern, int businessId)
    {
        try
        {
            var items = await _context.Customers
                .Where(c => c.BusinessId == businessId && c.IsActive)
                .Where(c => EF.Functions.Like(c.Name, likePattern)
                         || EF.Functions.Like(c.Email ?? "", likePattern))
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(5)
                .Select(c => new SearchResultItem
                {
                    Id = c.Id,
                    Primary = c.Name,
                    Secondary = c.Email ?? "",
                    Url = "/Customer/Detail/" + c.Id
                })
                .ToListAsync();

            return new SearchResultGroup { Type = "customer", Label = "Customers", Items = items };
        }
        catch (Exception ex) { return null; }
    }

    private async Task<SearchResultGroup?> SearchPurchasesAsync(string likePattern, int businessId)
    {
        try
        {
            var items = await _context.Purchases
                .Where(p => p.BusinessId == businessId && !p.IsCancelled)
                .Where(p => EF.Functions.Like(p.InvoiceNumber ?? "", likePattern)
                         || EF.Functions.Like(p.Description ?? "", likePattern)
                         || EF.Functions.Like(p.Supplier.Name, likePattern))
                .OrderByDescending(p => p.CreatedAtUtc)
                .Take(5)
                .Select(p => new SearchResultItem
                {
                    Id = p.Id,
                    Primary = p.InvoiceNumber ?? p.Description ?? "Purchase",
                    Secondary = p.Supplier.Name,
                    Url = "/Purchase/Edit/" + p.Id
                })
                .ToListAsync();

            return new SearchResultGroup { Type = "purchase", Label = "Purchases", Items = items };
        }
        catch (Exception ex) { return null; }
    }

    private async Task<SearchResultGroup?> SearchQuotationsAsync(string likePattern, int businessId)
    {
        try
        {
            var items = await _context.Quotations
                .Where(q => q.BusinessId == businessId && !q.IsDeleted)
                .Where(q => EF.Functions.Like(q.Reference, likePattern)
                         || EF.Functions.Like(q.Customer.Name, likePattern))
                .OrderByDescending(q => q.CreatedAtUtc)
                .Take(5)
                .Select(q => new SearchResultItem
                {
                    Id = q.Id,
                    Primary = q.Reference,
                    Secondary = q.Customer.Name,
                    Url = "/Quotation/Detail/" + q.Id
                })
                .ToListAsync();

            return new SearchResultGroup { Type = "quotation", Label = "Quotations", Items = items };
        }
        catch (Exception ex) { return null; }
    }

    private async Task<SearchResultGroup?> SearchSuppliersAsync(string likePattern, int businessId)
    {
        try
        {
            var items = await _context.Suppliers
                .Where(s => s.BusinessId == businessId && s.IsActive)
                .Where(s => EF.Functions.Like(s.Name, likePattern))
                .OrderByDescending(s => s.CreatedAtUtc)
                .Take(5)
                .Select(s => new SearchResultItem
                {
                    Id = s.Id,
                    Primary = s.Name,
                    Secondary = "Supplier",
                    Url = "/Supplier/Dashboard/" + s.Id
                })
                .ToListAsync();

            return new SearchResultGroup { Type = "supplier", Label = "Suppliers", Items = items };
        }
        catch (Exception ex) { return null; }
    }

    private async Task<SearchResultGroup?> SearchProductsAsync(string likePattern, int businessId)
    {
        try
        {
            var items = await _context.Products
                .Where(p => p.BusinessId == businessId && p.IsActive)
                .Where(p => EF.Functions.Like(p.Description, likePattern)
                         || EF.Functions.Like(p.ProductCode, likePattern))
                .OrderByDescending(p => p.CreatedAtUtc)
                .Take(5)
                .Select(p => new SearchResultItem
                {
                    Id = p.Id,
                    Primary = p.Description,
                    Secondary = p.ProductCode,
                    Url = "/Product/Edit/" + p.Id
                })
                .ToListAsync();

            return new SearchResultGroup { Type = "product", Label = "Products", Items = items };
        }
        catch (Exception ex) { return null; }
    }
}
