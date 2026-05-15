using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
public class BusinessController : Controller
{
    private readonly IBusinessService _businessService;

    public BusinessController(IBusinessService businessService)
    {
        _businessService = businessService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var businesses = await _businessService.GetAllBusinessesAsync();
        return View(businesses);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(string.Empty, "Business name is required.");
            return View();
        }

        var isUnique = await _businessService.IsBusinessNameUniqueAsync(name);
        if (!isUnique)
        {
            ModelState.AddModelError(string.Empty, "Business name already exists.");
            return View();
        }

        await _businessService.CreateBusinessAsync(name);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var business = await _businessService.GetBusinessByIdAsync(id);
        if (business == null) return NotFound();
        return View(business);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name)
    {
        var business = await _businessService.GetBusinessByIdAsync(id);
        if (business == null) return NotFound();

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(string.Empty, "Business name is required.");
            return View(business);
        }

        var isUnique = await _businessService.IsBusinessNameUniqueAsync(name, id);
        if (!isUnique)
        {
            ModelState.AddModelError(string.Empty, "Business name already exists.");
            return View(business);
        }

        business.Name = name;
        await _businessService.UpdateBusinessAsync(business);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _businessService.DeactivateBusinessAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Profile(int businessId)
    {
        var profile = await _businessService.GetBusinessProfileAsync(businessId);
        if (profile == null)
        {
            profile = new BusinessProfile { BusinessId = businessId };
        }
        var business = await _businessService.GetBusinessByIdAsync(businessId);
        ViewBag.BusinessName = business?.Name ?? "Unknown";
        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(BusinessProfile profile)
    {
        try
        {
            await _businessService.SaveBusinessProfileAsync(profile);
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var business = await _businessService.GetBusinessByIdAsync(profile.BusinessId);
            ViewBag.BusinessName = business?.Name ?? "Unknown";
            return View(profile);
        }
    }
}
