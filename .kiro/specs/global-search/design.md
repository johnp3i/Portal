# Design Document: Global Search

## Architecture Overview

Global Search follows the established Portal MVC pattern: a dedicated `SearchController` handles the AJAX endpoint, delegates to a `GlobalSearchService` in the Infrastructure layer, which runs parallel LINQ-to-SQL queries against `PortalDbContext`. The frontend is an inline `<script>` block in `_Layout.cshtml` that manages a debounced input, dropdown rendering, and keyboard navigation.

No new database tables are required — the feature queries existing entities (Invoice, Customer, Purchase, Quotation, Supplier, Product) using EF Core LIKE matching.

```
┌─────────────────────────────────────────────────────────────────┐
│  _Layout.cshtml (Search Bar + Dropdown)                         │
│  ┌──────────────────────────────────────────────────────┐       │
│  │ Debounced input → fetch /Search/AxGetGlobalSearch    │       │
│  └──────────────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  SearchController [Authorize]                                   │
│  ├─ AxGetGlobalSearch(string query)                             │
│  │   ├─ Validates query length ≥ 2                             │
│  │   ├─ Reads CurrentBusinessId from ICurrentTenantService     │
│  │   ├─ Reads PlanPermissions from HttpContext.Items            │
│  │   └─ Delegates to IGlobalSearchService.SearchAsync(...)     │
│  └─ Returns Json({ success, data })                            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  GlobalSearchService : IGlobalSearchService                     │
│  ├─ SearchAsync(query, businessId, permittedModules)            │
│  │   ├─ Determines which entity searchers to run               │
│  │   ├─ Executes Task.WhenAll(permitted entity queries)        │
│  │   ├─ Each query: .Where(LIKE match).Take(5).Select(dto)     │
│  │   └─ Aggregates non-empty groups into GlobalSearchResultDto │
│  └─ Fault isolation: failed queries excluded, others returned  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  PortalDbContext                                                │
│  ├─ Invoices (join Customer for CustomerName)                   │
│  ├─ Customers                                                   │
│  ├─ Purchases (join Supplier for SupplierName)                  │
│  ├─ Quotations (join Customer for CustomerName)                 │
│  ├─ Suppliers                                                   │
│  └─ Products (ProductCode = SKU, Description = Name)            │
└─────────────────────────────────────────────────────────────────┘
```

## Components

### 1. SearchController

**Location:** `Portal.Web/Controllers/SearchController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

[Authorize]
public class SearchController : Controller
{
    private readonly IGlobalSearchService _globalSearchService;
    private readonly ICurrentTenantService _currentTenantService;

    public SearchController(
        IGlobalSearchService globalSearchService,
        ICurrentTenantService currentTenantService)
    {
        _globalSearchService = globalSearchService;
        _currentTenantService = currentTenantService;
    }

    [HttpGet]
    public async Task<IActionResult> AxGetGlobalSearch(string? query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return Json(new { success = true, data = new { groups = Array.Empty<object>() } });
            }

            var businessId = _currentTenantService.CurrentBusinessId;

            // Read permitted modules from PlanPermissionFilter (set in HttpContext.Items)
            var permittedModules = HttpContext.Items["PlanPermissions"] as List<string>
                ?? new List<string>();

            var result = await _globalSearchService.SearchAsync(
                query.Trim(), businessId, permittedModules.ToHashSet());

            return Json(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Search is temporarily unavailable. Please try again." });
        }
    }
}
```

**Key decisions:**
- No `[ModuleAccess]` attribute — Search is a cross-module feature available to all authenticated users. Results are filtered by permission, not access to the controller.
- Added to `PlanPermissionFilter.NonModuleControllers` so the filter does not block it, while still reading `HttpContext.Items["PlanPermissions"]` set by the filter.
- Returns empty result (not error) for short queries — avoids unnecessary DB load.

### 2. IGlobalSearchService / GlobalSearchService

**Location:** `Portal.Infrastructure/Services/IGlobalSearchService.cs` and `Portal.Infrastructure/Services/GlobalSearchService.cs`

```csharp
namespace Portal.Infrastructure.Services;

public interface IGlobalSearchService
{
    Task<GlobalSearchResultDto> SearchAsync(string query, int businessId, HashSet<string> permittedModules);
}
```

```csharp
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;

namespace Portal.Infrastructure.Services;

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
            tasks.Add(SearchPurchasesAsync(likePattern, businessId));

        if (permittedModules.Contains(PortalModules.Quotation))
            tasks.Add(SearchQuotationsAsync(likePattern, businessId));

        if (permittedModules.Contains(PortalModules.Purchase))
            tasks.Add(SearchSuppliersAsync(likePattern, businessId));

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
                    Url = $"/Invoice/Detail/{i.Id}"
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
                    Url = $"/Customer/Detail/{c.Id}"
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
                    Url = $"/Purchase/Edit/{p.Id}"
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
                    Url = $"/Quotation/Detail/{q.Id}"
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
                    Secondary = "",
                    Url = $"/Supplier/Dashboard/{s.Id}"
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
                    Url = $"/Product/Edit/{p.Id}"
                })
                .ToListAsync();

            return new SearchResultGroup { Type = "product", Label = "Products", Items = items };
        }
        catch (Exception ex) { return null; }
    }
}
```

**Key decisions:**
- Each entity query is wrapped in its own try/catch returning `null` on failure — fault isolation ensures one broken query doesn't take down the entire search.
- `EF.Functions.Like` ensures SQL parameterization (EF Core handles escaping), preventing SQL injection.
- `.Take(5)` is applied before materialisation — the SQL generated uses `TOP 5`, avoiding excess row fetches.
- Product entity uses `ProductCode` (the SKU equivalent) and `Description` (the product name).
- Quotation uses `Reference` (the quotation number field).
- Supplier search is gated behind `PortalModules.Purchase` since suppliers are part of the Purchase module.

### 3. Result DTOs

**Location:** `Portal.Infrastructure/Services/GlobalSearchResultDto.cs`

```csharp
namespace Portal.Infrastructure.Services;

public class GlobalSearchResultDto
{
    public List<SearchResultGroup> Groups { get; set; } = new();
}

public class SearchResultGroup
{
    public string Type { get; set; } = null!;
    public string Label { get; set; } = null!;
    public List<SearchResultItem> Items { get; set; } = new();
}

public class SearchResultItem
{
    public int Id { get; set; }
    public string Primary { get; set; } = null!;
    public string Secondary { get; set; } = null!;
    public string Url { get; set; } = null!;
}
```

### 4. Frontend Component

**Location:** Inline `<script>` and HTML in `Portal.Web/Views/Shared/_Layout.cshtml` (after the account dropdown, inside `<main class="content">`)

```html
<!-- Global Search Bar -->
<div id="globalSearchContainer" style="position:absolute;top:25px;left:24px;z-index:100;width:320px;">
    <div style="position:relative;">
        <input type="text" id="globalSearchInput"
               placeholder="Search invoices, customers, products..."
               autocomplete="off"
               style="width:100%;padding:9px 14px 9px 36px;border:1px solid rgba(13,94,166,.12);border-radius:12px;font-size:13px;font-family:'Inter',sans-serif;background:#fff;outline:none;transition:border-color .2s,box-shadow .2s;" />
        <svg style="position:absolute;left:11px;top:50%;transform:translateY(-50%);pointer-events:none;" width="16" height="16" fill="none" stroke="#8899A6" stroke-width="2" viewBox="0 0 24 24">
            <circle cx="11" cy="11" r="8"/><path d="M21 21l-4.35-4.35"/>
        </svg>
        <kbd style="position:absolute;right:10px;top:50%;transform:translateY(-50%);font-size:11px;color:#8899A6;background:rgba(13,94,166,.05);border:1px solid rgba(13,94,166,.10);border-radius:5px;padding:2px 6px;pointer-events:none;">Ctrl+K</kbd>
    </div>
    <!-- Search Dropdown -->
    <div id="globalSearchDropdown" style="display:none;position:absolute;top:100%;left:0;right:0;margin-top:6px;background:#fff;border:1px solid rgba(13,94,166,.10);border-radius:12px;box-shadow:0 8px 24px rgba(13,94,166,.12);max-height:420px;overflow-y:auto;z-index:1001;">
    </div>
</div>
```

```javascript
(function() {
    const input = document.getElementById('globalSearchInput');
    const dropdown = document.getElementById('globalSearchDropdown');
    let debounceTimer = null;
    let highlightIndex = -1;
    let currentItems = [];

    // Ctrl+K shortcut
    document.addEventListener('keydown', function(e) {
        if (e.ctrlKey && e.key === 'k') {
            e.preventDefault();
            input.focus();
        }
    });

    // Debounced search
    input.addEventListener('input', function() {
        clearTimeout(debounceTimer);
        const query = input.value.trim();

        if (query.length < 2) {
            showEmpty();
            return;
        }

        showLoading();
        debounceTimer = setTimeout(() => executeSearch(query), 300);
    });

    input.addEventListener('focus', function() {
        dropdown.style.display = 'block';
        if (!input.value.trim()) showEmpty();
    });

    // Keyboard navigation
    input.addEventListener('keydown', function(e) {
        if (e.key === 'ArrowDown') { e.preventDefault(); moveHighlight(1); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); moveHighlight(-1); }
        else if (e.key === 'Enter' && highlightIndex >= 0) { e.preventDefault(); navigateToHighlighted(); }
        else if (e.key === 'Escape') { closeDropdown(); }
    });

    // Click outside to dismiss
    document.addEventListener('click', function(e) {
        var container = document.getElementById('globalSearchContainer');
        if (container && !container.contains(e.target)) {
            closeDropdown();
        }
    });

    async function executeSearch(query) {
        try {
            const response = await fetch('/Search/AxGetGlobalSearch?query=' + encodeURIComponent(query));
            const result = await response.json();

            if (!response.ok || !result.success) {
                showError();
                return;
            }

            renderResults(result.data.groups || []);
        } catch (e) {
            showError();
        }
    }

    function renderResults(groups) {
        highlightIndex = -1;
        currentItems = [];

        if (groups.length === 0) {
            dropdown.innerHTML = '<div style="padding:18px;text-align:center;color:#8899A6;font-size:13px;">No results found</div>';
            dropdown.style.display = 'block';
            return;
        }

        let html = '';
        groups.forEach(group => {
            html += '<div style="padding:8px 14px 4px;font-size:11px;font-weight:700;color:#5E7385;text-transform:uppercase;letter-spacing:.5px;">' + escapeHtml(group.label) + '</div>';
            group.items.forEach(item => {
                const idx = currentItems.length;
                currentItems.push(item);
                html += '<a href="' + escapeHtml(item.url) + '" class="gs-item" data-idx="' + idx + '" style="display:block;padding:8px 14px;text-decoration:none;border-radius:8px;margin:2px 6px;transition:background .15s;">';
                html += '<div style="font-size:13px;font-weight:600;color:#0B1B28;">' + escapeHtml(item.primary) + '</div>';
                if (item.secondary) html += '<div style="font-size:12px;color:#5E7385;margin-top:1px;">' + escapeHtml(item.secondary) + '</div>';
                html += '</a>';
            });
        });

        dropdown.innerHTML = html;
        dropdown.style.display = 'block';
    }

    function moveHighlight(direction) {
        if (currentItems.length === 0) return;
        highlightIndex += direction;
        if (highlightIndex < 0) highlightIndex = currentItems.length - 1;
        if (highlightIndex >= currentItems.length) highlightIndex = 0;
        updateHighlight();
    }

    function updateHighlight() {
        dropdown.querySelectorAll('.gs-item').forEach((el, i) => {
            el.style.background = i === highlightIndex ? 'rgba(13,94,166,.06)' : '';
        });
    }

    function navigateToHighlighted() {
        if (highlightIndex >= 0 && currentItems[highlightIndex]) {
            window.location.href = currentItems[highlightIndex].url;
        }
    }

    function showEmpty() {
        dropdown.innerHTML = '';
        dropdown.style.display = 'block';
    }

    function showLoading() {
        dropdown.innerHTML = '<div style="padding:18px;text-align:center;color:#8899A6;font-size:13px;">Searching...</div>';
        dropdown.style.display = 'block';
    }

    function showError() {
        dropdown.innerHTML = '<div style="padding:18px;text-align:center;color:#C24A4A;font-size:13px;">Search unavailable. Please try again.</div>';
        dropdown.style.display = 'block';
    }

    function closeDropdown() {
        dropdown.style.display = 'none';
        highlightIndex = -1;
        input.blur();
    }

    function escapeHtml(str) {
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }
})();
```

**Key decisions:**
- No BlockUI — search uses inline loading indicator per Requirement 15.
- No SweetAlert2 for search errors — inline error message within the dropdown.
- `escapeHtml()` prevents XSS from entity names rendered in the dropdown.
- Debounce timer resets on each keystroke, discarding stale requests.
- Keyboard navigation wraps around (top ↔ bottom).

### 5. DI Registration

**Location:** `Portal.Web/Program.cs` (service registration section)

```csharp
builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();
```

### 6. PlanPermissionFilter Exemption

Add `"Search"` to the `NonModuleControllers` HashSet in `PlanPermissionFilter.cs` so the filter allows the request through while still populating `HttpContext.Items["PlanPermissions"]`.

## Data Flow

1. User types in search bar → 300ms debounce elapses
2. Frontend sends `GET /Search/AxGetGlobalSearch?query=acme`
3. `SearchController.AxGetGlobalSearch` validates query length
4. Controller reads `CurrentBusinessId` (from claims via `ICurrentTenantService`)
5. Controller reads `PlanPermissions` from `HttpContext.Items` (set by `PlanPermissionFilter`)
6. Controller calls `_globalSearchService.SearchAsync("acme", 42, {"invoice","customer",...})`
7. Service builds per-entity query tasks based on permitted modules
8. `Task.WhenAll` executes queries in parallel
9. Each query: `WHERE BusinessId = @p0 AND (Column LIKE @p1 OR ...) ORDER BY CreatedAtUtc DESC` with `TOP 5`
10. Failed queries return `null` (caught individually)
11. Service aggregates non-null, non-empty groups into `GlobalSearchResultDto`
12. Controller serialises to JSON: `{ success: true, data: { groups: [...] } }`
13. Frontend renders grouped results in dropdown

## Error Handling

| Layer | Strategy |
|-------|----------|
| Controller | try/catch wrapping entire action; returns `{ success: false, message: "..." }` on exception |
| Service (per-entity) | Individual try/catch per entity query; returns `null` to exclude failed entity from results |
| Frontend | Network/parse errors caught in `executeSearch`; shows inline error in dropdown (no alert/BlockUI) |

## Security Considerations

- **SQL Injection**: `EF.Functions.Like` with parameterised queries. EF Core generates `@p0`, `@p1` parameters — no string concatenation.
- **Tenant Isolation**: Every query includes `WHERE BusinessId = @businessId`. The `businessId` is derived from authenticated claims, not user input.
- **Module Gating**: Only queries for modules the user has plan access to are executed. Results cannot leak entities from unpermitted modules.
- **XSS**: Frontend escapes all entity data before rendering in HTML via `escapeHtml()`.
- **No new attack surface**: The endpoint is `[Authorize]`-only and returns read-only data.

## Entity-to-Module Mapping

| Entity | Module Constant | Search Fields | URL Pattern |
|--------|----------------|---------------|-------------|
| Invoice | `PortalModules.Invoice` | InvoiceNumber, Customer.Name | /Invoice/Detail/{id} |
| Customer | `PortalModules.Customer` | Name, Email | /Customer/Detail/{id} |
| Purchase | `PortalModules.Purchase` | InvoiceNumber, Description, Supplier.Name | /Purchase/Edit/{id} |
| Quotation | `PortalModules.Quotation` | Reference, Customer.Name | /Quotation/Detail/{id} |
| Supplier | `PortalModules.Purchase` | Name | /Supplier/Dashboard/{id} |
| Product | `PortalModules.Products` | Description, ProductCode | /Product/Edit/{id} |

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Short query rejection

*For any* query string where the trimmed length is less than 2 characters (including null, empty string, single character, and whitespace-only strings), the search endpoint SHALL return a successful response with an empty groups array and SHALL NOT execute any database queries.

**Validates: Requirements 4.2**

### Property 2: Tenant isolation

*For any* search query and any business context, every `SearchResultItem` in the response SHALL belong to the `CurrentBusinessId` of the authenticated user. No result item from a different business SHALL ever appear in the response.

**Validates: Requirements 5.1, 5.2**

### Property 3: Module permission filtering

*For any* search query and any set of permitted modules, the response SHALL only contain `SearchResultGroup` entries whose entity type maps to a module present in the permitted set. No group corresponding to a non-permitted module SHALL appear in the results.

**Validates: Requirements 6.2, 6.3**

### Property 4: Search field matching correctness

*For any* entity returned in search results, at least one of its searchable fields SHALL contain the query string (case-insensitive substring match). Specifically: for Invoices the match is in InvoiceNumber or CustomerName; for Customers in Name or Email; for Purchases in InvoiceNumber, Description, or SupplierName; for Quotations in Reference or CustomerName; for Suppliers in Name; for Products in Description or ProductCode.

**Validates: Requirements 8.1, 8.2, 8.3, 8.4, 8.5, 8.6**

### Property 5: Result limit invariant

*For any* search query and any amount of matching data in the database, each `SearchResultGroup` in the response SHALL contain at most 5 items.

**Validates: Requirements 9.1, 9.2**

### Property 6: Empty groups omitted

*For any* search response, every `SearchResultGroup` present in the `Groups` list SHALL have at least one item. No group with zero items SHALL be included in the response.

**Validates: Requirements 10.3**

### Property 7: Fault tolerance

*For any* set of entity queries where one or more queries throw an exception, the response SHALL still contain the results from all non-failing queries. A single entity query failure SHALL NOT cause the entire search to fail or return an error.

**Validates: Requirements 7.3**
