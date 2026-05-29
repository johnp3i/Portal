using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public ProductController(
        IProductService productService,
        IProductAutocompleteService autocompleteService,
        ISupplierService supplierService,
        ProductTypeRepository productTypeRepository)
    {
        _productService = productService;
        _autocompleteService = autocompleteService;
        _supplierService = supplierService;
        _productTypeRepository = productTypeRepository;
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

        ViewBag.PagedResult = pagedResult;
        ViewBag.SearchTerm = search;
        ViewBag.Kpis = kpis;
        ViewBag.TopProducts = topProducts;
        ViewBag.Suppliers = suppliers;
        ViewBag.ProductTypes = productTypes;

        return View(pagedResult.Items);
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
}
