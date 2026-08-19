using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Products)]
public class ProductController : Controller
{
    private readonly IProductService _productService;
    private readonly IProductAutocompleteService _autocompleteService;
    private readonly ISupplierService _supplierService;
    private readonly ProductTypeRepository _productTypeRepository;
    private readonly IProductPriceTierService _priceTierService;
    private readonly ICurrentTenantService _tenantService;

    public ProductController(
        IProductService productService,
        IProductAutocompleteService autocompleteService,
        ISupplierService supplierService,
        ProductTypeRepository productTypeRepository,
        IProductPriceTierService priceTierService,
        ICurrentTenantService tenantService)
    {
        _productService = productService;
        _autocompleteService = autocompleteService;
        _supplierService = supplierService;
        _productTypeRepository = productTypeRepository;
        _priceTierService = priceTierService;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        if (search != null && search.Length > 200)
            search = search[..200];

        var pagedResult = await _productService.GetProductsPagedAsync(search, page);
        var kpis = await _productService.GetKpisAsync();
        var topProducts = await _productService.GetTopProductsByUsageAsync(10);
        var suppliers = await _supplierService.GetActiveSuppliersAsync();
        var productTypes = await _productTypeRepository.GetAllAsync();

        // Active tier counts for the products on this page (single batched query, no N+1)
        var tierCounts = await _priceTierService.GetActiveTierCountsAsync(
            pagedResult.Items.Select(p => p.Id));

        ViewBag.PagedResult = pagedResult;
        ViewBag.SearchTerm = search;
        ViewBag.Kpis = kpis;
        ViewBag.TopProducts = topProducts;
        ViewBag.Suppliers = suppliers;
        ViewBag.ProductTypes = productTypes;
        ViewBag.TierCounts = tierCounts;

        return View(pagedResult.Items);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null) return NotFound();

        var insightsService = HttpContext.RequestServices.GetRequiredService<IProductInsightsService>();
        var planCheckService = HttpContext.RequestServices.GetRequiredService<IPlanCheckService>();
        var tenantService = HttpContext.RequestServices.GetRequiredService<ICurrentTenantService>();
        var dbContext = HttpContext.RequestServices.GetRequiredService<Portal.Infrastructure.Data.PortalDbContext>();

        var businessId = tenantService.CurrentBusinessId;
        if (product.BusinessId != businessId) return NotFound();

        var profile = await dbContext.BusinessProfiles.IgnoreQueryFilters()
            .Where(bp => bp.BusinessId == businessId)
            .Select(bp => new { bp.CurrencySymbol })
            .FirstOrDefaultAsync();

        var kpis = await insightsService.GetSalesKpisAsync(product.ProductCode, businessId, product.DefaultCostPrice);
        var topCustomers = await insightsService.GetTopCustomersAsync(product.ProductCode, businessId);
        var customerSummary = await insightsService.GetCustomerSummaryAsync(product.ProductCode, businessId);
        var trend = await insightsService.GetMonthlyTrendAsync(product.ProductCode, businessId);
        var priceHistory = await dbContext.ProductPriceHistories
            .Where(h => h.ProductId == id)
            .OrderByDescending(h => h.EffectiveFromUtc)
            .ToListAsync();

        var isProfessional = await planCheckService.IsModuleInPlanAsync(PortalModules.Cashflow);

        Portal.Infrastructure.Models.ProductInsights.ProductForecastDto? forecast = null;
        if (isProfessional)
            forecast = await insightsService.GetForecastAsync(product.ProductCode, businessId, product.DefaultSellingPrice);

        var pipeline = await insightsService.GetPipelineActivityAsync(id, businessId);

        var supplierName = product.SupplierId.HasValue
            ? (await dbContext.Suppliers.IgnoreQueryFilters().Where(s => s.Id == product.SupplierId.Value).Select(s => s.Name).FirstOrDefaultAsync())
            : null;

        string? productTypeName = null;
        if (product.ProductTypeId.HasValue)
        {
            var productTypeRepo = HttpContext.RequestServices.GetRequiredService<Portal.Infrastructure.Repositories.ProductTypeRepository>();
            var types = await productTypeRepo.GetAllAsync();
            productTypeName = types.FirstOrDefault(t => t.Id == product.ProductTypeId.Value)?.Name;
        }

        var model = new Portal.Infrastructure.Models.ProductInsights.ProductDetailViewModel
        {
            Product = product,
            SupplierName = supplierName,
            ProductTypeName = productTypeName,
            CurrencySymbol = profile?.CurrencySymbol ?? "€",
            Kpis = kpis,
            TopCustomers = topCustomers,
            CustomerSummary = customerSummary,
            MonthlyTrend = trend,
            PriceHistory = priceHistory,
            Forecast = forecast,
            Pipeline = pipeline,
            IsProfessional = isProfessional
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Autocomplete(string query)
    {
        try
        {
            var results = await _autocompleteService.SearchAsync(query);
            return Json(results);
        }
        catch (Exception)
        {
            return Json(new List<AutocompleteResultDto>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] string productCode,
        [FromForm] string description,
        [FromForm] decimal defaultSellingPrice,
        [FromForm] decimal defaultCostPrice,
        [FromForm] decimal defaultVatRate,
        [FromForm] int? supplierId,
        [FromForm] int? productTypeId,
        [FromForm] bool isActive = true)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        var product = new Product
        {
            ProductCode = productCode,
            Description = description,
            DefaultSellingPrice = defaultSellingPrice,
            DefaultCostPrice = defaultCostPrice,
            DefaultVatRate = defaultVatRate,
            SupplierId = supplierId,
            ProductTypeId = productTypeId,
            IsActive = isActive
        };

        try
        {
            var result = await _productService.CreateProductAsync(product, userId);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (ArgumentException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        [FromForm] int id,
        [FromForm] string productCode,
        [FromForm] string description,
        [FromForm] decimal defaultSellingPrice,
        [FromForm] decimal defaultCostPrice,
        [FromForm] decimal defaultVatRate,
        [FromForm] int? supplierId,
        [FromForm] int? productTypeId,
        [FromForm] bool isActive = true)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        var product = new Product
        {
            Id = id,
            ProductCode = productCode,
            Description = description,
            DefaultSellingPrice = defaultSellingPrice,
            DefaultCostPrice = defaultCostPrice,
            DefaultVatRate = defaultVatRate,
            SupplierId = supplierId,
            ProductTypeId = productTypeId,
            IsActive = isActive
        };

        try
        {
            var result = await _productService.UpdateProductAsync(product, userId);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (ArgumentException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate([FromForm] int productId)
    {
        var result = await _productService.DeactivateProductAsync(productId);
        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> GetProduct(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
            return Json(new { success = false, message = "Product not found." });

        return Json(new
        {
            success = true,
            product = new
            {
                product.Id,
                product.ProductCode,
                product.Description,
                product.DefaultSellingPrice,
                product.DefaultCostPrice,
                product.DefaultVatRate,
                product.SupplierId,
                product.ProductTypeId,
                product.IsActive,
                product.LastUsedDate
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> PriceHistory(int productId)
    {
        var history = await _productService.GetPriceHistoryAsync(productId);

        if (history == null || history.Count == 0)
            return Json(new { success = true, message = "No price changes have been recorded.", records = Array.Empty<object>() });

        var records = history.Select(h => new
        {
            h.SellingPrice,
            h.CostPrice,
            effectiveFromUtc = h.EffectiveFromUtc.ToString("dd MMM yyyy HH:mm"),
            h.ChangedByUserId
        }).ToList();

        return Json(new { success = true, records });
    }

    // ─── Price Tier Management Endpoints ─────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateTier([FromBody] CreateTierRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            var result = await _priceTierService.CreateTierAsync(request, businessId, userId);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Price tier created successfully.", tier = new { id = result.Id } });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateTier([FromBody] UpdateTierRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            var result = await _priceTierService.UpdateTierAsync(request, businessId, userId);

            return Json(new { success = result.Success, message = result.Message ?? "Price tier updated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSetDefaultTier([FromBody] SetDefaultTierRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var result = await _priceTierService.SetDefaultTierAsync(request.TierId, request.ProductId, businessId);

            return Json(new { success = result.Success, message = result.Message ?? "Default tier updated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeactivateTier([FromBody] DeactivateTierRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var result = await _priceTierService.DeactivateTierAsync(request.TierId, request.ProductId, businessId);

            return Json(new { success = result.Success, message = result.Message ?? "Price tier deactivated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostReactivateTier([FromBody] ReactivateTierRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var result = await _priceTierService.ReactivateTierAsync(request.TierId, request.ProductId, businessId);

            return Json(new { success = result.Success, message = result.Message ?? "Price tier reactivated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetProductTiers(int productId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var tiers = await _priceTierService.GetTiersForProductAsync(productId, businessId);

            var data = tiers.Select(t => new
            {
                t.Id,
                t.TierName,
                t.SellingPrice,
                t.CostPrice,
                t.IsDefault,
                t.IsActive,
                t.CreatedAtUtc,
                t.UpdatedAtUtc
            }).ToList();

            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }
}
