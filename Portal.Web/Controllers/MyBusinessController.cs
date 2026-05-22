using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

/// <summary>
/// Controller for regular business users to manage their own business profile, logo library, and payment details.
/// Tab-based view: Profile | Logos | Payment Details
/// </summary>
[Authorize]
public class MyBusinessController : Controller
{
    private readonly IBusinessService _businessService;
    private readonly ILogoService _logoService;
    private readonly ICurrentTenantService _tenantService;
    private readonly BusinessPaymentDetailRepository _paymentDetailRepository;

    public MyBusinessController(IBusinessService businessService, ILogoService logoService,
        ICurrentTenantService tenantService, BusinessPaymentDetailRepository paymentDetailRepository)
    {
        _businessService = businessService;
        _logoService = logoService;
        _tenantService = tenantService;
        _paymentDetailRepository = paymentDetailRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string tab = "profile")
    {
        var businessId = _tenantService.CurrentBusinessId;
        var business = await _businessService.GetBusinessByIdAsync(businessId);
        var profile = await _businessService.GetBusinessProfileAsync(businessId);
        var logos = await _logoService.GetByBusinessIdAsync(businessId);
        var paymentDetails = await _paymentDetailRepository.GetByBusinessIdAsync(businessId);

        ViewBag.BusinessName = business?.Name ?? "Unknown";
        ViewBag.ActiveTab = tab;
        ViewBag.Logos = logos;
        ViewBag.PaymentDetails = paymentDetails;

        if (profile == null)
        {
            profile = new BusinessProfile { BusinessId = businessId };
        }

        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(BusinessProfile profile)
    {
        profile.BusinessId = _tenantService.CurrentBusinessId;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(profile.CompanyRegistrationNumber) ||
            string.IsNullOrWhiteSpace(profile.VatRegistrationNumber) ||
            string.IsNullOrWhiteSpace(profile.AddressLine1) ||
            string.IsNullOrWhiteSpace(profile.City) ||
            string.IsNullOrWhiteSpace(profile.PostalCode) ||
            string.IsNullOrWhiteSpace(profile.Country) ||
            string.IsNullOrWhiteSpace(profile.Email) ||
            profile.VatRegistrationDate == default)
        {
            TempData["Error"] = "Please fill in all required fields (marked with *).";

            // Return the view with the submitted data so the user doesn't lose their input
            var businessId = _tenantService.CurrentBusinessId;
            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var logos = await _logoService.GetByBusinessIdAsync(businessId);

            ViewBag.BusinessName = business?.Name ?? "Unknown";
            ViewBag.ActiveTab = "profile";
            ViewBag.Logos = logos;

            return View("Index", profile);
        }

        try
        {
            await _businessService.SaveBusinessProfileAsync(profile);
            TempData["Success"] = "Business profile updated successfully.";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;

            var businessId = _tenantService.CurrentBusinessId;
            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var logos = await _logoService.GetByBusinessIdAsync(businessId);

            ViewBag.BusinessName = business?.Name ?? "Unknown";
            ViewBag.ActiveTab = "profile";
            ViewBag.Logos = logos;

            return View("Index", profile);
        }

        return RedirectToAction(nameof(Index), new { tab = "profile" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadLogo(IFormFile file, string displayName)
    {
        if (file == null || string.IsNullOrWhiteSpace(displayName))
        {
            TempData["Error"] = "File and display name are required.";
            return RedirectToAction(nameof(Index), new { tab = "logos" });
        }

        try
        {
            await _logoService.UploadAsync(_tenantService.CurrentBusinessId, file, displayName.Trim());
            TempData["Success"] = "Logo uploaded successfully.";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "logos" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLogo(int id)
    {
        try
        {
            await _logoService.DeleteAsync(id, _tenantService.CurrentBusinessId);
            TempData["Success"] = "Logo deleted successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "logos" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryLogo(int id)
    {
        try
        {
            await _logoService.SetPrimaryAsync(id, _tenantService.CurrentBusinessId);
            TempData["Success"] = "Primary logo updated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "logos" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPaymentDetail(string label, string bankName, string iban, string payeeName)
    {
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(bankName) ||
            string.IsNullOrWhiteSpace(iban) || string.IsNullOrWhiteSpace(payeeName))
        {
            TempData["Error"] = "All payment detail fields are required.";
            return RedirectToAction(nameof(Index), new { tab = "payment" });
        }

        var businessId = _tenantService.CurrentBusinessId;
        var existing = await _paymentDetailRepository.GetByBusinessIdAsync(businessId);

        var detail = new BusinessPaymentDetail
        {
            BusinessId = businessId,
            Label = label.Trim(),
            BankName = bankName.Trim(),
            Iban = iban.Trim(),
            PayeeName = payeeName.Trim(),
            SortOrder = existing.Count + 1
        };

        await _paymentDetailRepository.InsertAsync(detail);
        TempData["Success"] = "Payment detail added successfully.";

        return RedirectToAction(nameof(Index), new { tab = "payment" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePaymentDetail(int id)
    {
        await _paymentDetailRepository.DeleteAsync(id, _tenantService.CurrentBusinessId);
        TempData["Success"] = "Payment detail removed.";
        return RedirectToAction(nameof(Index), new { tab = "payment" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePaymentDetail(int id, string label, string bankName, string iban, string payeeName)
    {
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(bankName) ||
            string.IsNullOrWhiteSpace(iban) || string.IsNullOrWhiteSpace(payeeName))
        {
            TempData["Error"] = "All payment detail fields are required.";
            return RedirectToAction(nameof(Index), new { tab = "payment" });
        }

        await _paymentDetailRepository.UpdateAsync(id, _tenantService.CurrentBusinessId,
            label.Trim(), bankName.Trim(), iban.Trim(), payeeName.Trim());
        TempData["Success"] = "Payment detail updated.";
        return RedirectToAction(nameof(Index), new { tab = "payment" });
    }
}
