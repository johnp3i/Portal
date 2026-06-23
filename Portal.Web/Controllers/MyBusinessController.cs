using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using System.Security.Claims;

namespace Portal.Web.Controllers;

/// <summary>
/// Controller for regular business users to manage their own business profile, logo library, and payment details.
/// Tab-based view: Profile | Logos | Payment Details
/// Non-owner users have read-only access.
/// </summary>
[Authorize]
public class MyBusinessController : Controller
{
    private readonly IBusinessService _businessService;
    private readonly ILogoService _logoService;
    private readonly ICurrentTenantService _tenantService;
    private readonly BusinessPaymentDetailRepository _paymentDetailRepository;
    private readonly IPlanCheckService _planCheckService;
    private readonly MembershipDbContext _membershipDbContext;

    public MyBusinessController(IBusinessService businessService, ILogoService logoService,
        ICurrentTenantService tenantService, BusinessPaymentDetailRepository paymentDetailRepository,
        IPlanCheckService planCheckService, MembershipDbContext membershipDbContext)
    {
        _businessService = businessService;
        _logoService = logoService;
        _tenantService = tenantService;
        _paymentDetailRepository = paymentDetailRepository;
        _planCheckService = planCheckService;
        _membershipDbContext = membershipDbContext;
    }

    private bool CanEdit()
    {
        return User.IsInRole("SuperAdmin") || User.HasClaim("IsOwner", "true");
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
        ViewBag.IsReadOnly = !CanEdit();

        if (profile == null)
        {
            profile = new BusinessProfile { BusinessId = businessId };
        }

        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(BusinessProfile profile, string? businessName)
    {
        if (!CanEdit())
        {
            TempData["Error"] = "You do not have permission to edit business settings.";
            return RedirectToAction(nameof(Index), new { tab = "profile" });
        }

        profile.BusinessId = _tenantService.CurrentBusinessId;

        // Update business name if provided
        if (!string.IsNullOrWhiteSpace(businessName))
        {
            var business = await _businessService.GetBusinessByIdAsync(profile.BusinessId);
            if (business != null && business.Name != businessName.Trim())
            {
                business.Name = businessName.Trim();
                await _businessService.UpdateBusinessAsync(business);
            }
        }

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
        if (!CanEdit())
        {
            TempData["Error"] = "You do not have permission to edit business settings.";
            return RedirectToAction(nameof(Index), new { tab = "logos" });
        }

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
        if (!CanEdit())
        {
            TempData["Error"] = "You do not have permission to edit business settings.";
            return RedirectToAction(nameof(Index), new { tab = "logos" });
        }

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
        if (!CanEdit())
        {
            TempData["Error"] = "You do not have permission to edit business settings.";
            return RedirectToAction(nameof(Index), new { tab = "logos" });
        }

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
        if (!CanEdit())
        {
            TempData["Error"] = "You do not have permission to edit business settings.";
            return RedirectToAction(nameof(Index), new { tab = "payment" });
        }

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
        if (!CanEdit())
        {
            TempData["Error"] = "You do not have permission to edit business settings.";
            return RedirectToAction(nameof(Index), new { tab = "payment" });
        }

        await _paymentDetailRepository.DeleteAsync(id, _tenantService.CurrentBusinessId);
        TempData["Success"] = "Payment detail removed.";
        return RedirectToAction(nameof(Index), new { tab = "payment" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePaymentDetail(int id, string label, string bankName, string iban, string payeeName)
    {
        if (!CanEdit())
        {
            TempData["Error"] = "You do not have permission to edit business settings.";
            return RedirectToAction(nameof(Index), new { tab = "payment" });
        }

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

    [HttpGet]
    public async Task<IActionResult> UserPermissions()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            // Get all UserBusiness records for the current business
            var userBusinesses = await _membershipDbContext.UserBusinesses
                .Include(ub => ub.User)
                .Where(ub => ub.BusinessId == businessId && ub.IsActive)
                .ToListAsync();

            // Get all permissions for these UserBusiness records
            var userBusinessIds = userBusinesses.Select(ub => ub.Id).ToList();
            var permissions = await _membershipDbContext.UserBusinessPermissions
                .Where(p => userBusinessIds.Contains(p.UserBusinessId))
                .ToListAsync();

            // Get plan modules
            var planModules = await _planCheckService.GetPlanModulesAsync();

            ViewBag.UserBusinesses = userBusinesses;
            ViewBag.Permissions = permissions;
            ViewBag.PlanModules = planModules;
            ViewBag.IsReadOnly = !CanEdit();

            return View();
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Failed to load user permissions.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostGrantPermission([FromBody] GrantPermissionRequest request)
    {
        try
        {
            if (!CanEdit())
            {
                return Json(new { success = false, message = "You do not have permission to manage user access." });
            }

            // Validate module is valid
            if (!PortalModules.IsValid(request.Module))
            {
                return Json(new { success = false, message = "Invalid module specified." });
            }

            // Validate access level is valid
            if (!AccessLevels.IsValid(request.AccessLevel))
            {
                return Json(new { success = false, message = "Invalid access level specified." });
            }

            // Validate module is in plan
            var isInPlan = await _planCheckService.IsModuleInPlanAsync(request.Module);
            if (!isInPlan)
            {
                return Json(new { success = false, message = "This module is not included in your current subscription plan." });
            }

            var businessId = _tenantService.CurrentBusinessId;

            // Check if target user is the owner — owners cannot have permissions modified
            var targetUserBusiness = await _membershipDbContext.UserBusinesses
                .FirstOrDefaultAsync(ub => ub.UserId == request.UserId && ub.BusinessId == businessId && ub.IsActive);

            if (targetUserBusiness == null)
            {
                return Json(new { success = false, message = "User not found in this business." });
            }

            if (targetUserBusiness.IsOwner)
            {
                return Json(new { success = false, message = "Cannot modify permissions for the business owner." });
            }

            // Find or create UserBusinessPermission
            var permission = await _membershipDbContext.UserBusinessPermissions
                .FirstOrDefaultAsync(p => p.UserBusinessId == targetUserBusiness.Id && p.Module == request.Module);

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (permission == null)
            {
                permission = new UserBusinessPermission
                {
                    UserBusinessId = targetUserBusiness.Id,
                    Module = request.Module,
                    AccessLevel = request.AccessLevel,
                    GrantedByUserId = currentUserId,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _membershipDbContext.UserBusinessPermissions.Add(permission);
            }
            else
            {
                permission.AccessLevel = request.AccessLevel;
                permission.GrantedByUserId = currentUserId;
                permission.IsActive = true;
                permission.DeactivatedAtUtc = null;
            }

            await _membershipDbContext.SaveChangesAsync();

            return Json(new { success = true, message = "Permission granted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred while granting permission." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostRevokePermission([FromBody] RevokePermissionRequest request)
    {
        try
        {
            if (!CanEdit())
            {
                return Json(new { success = false, message = "You do not have permission to manage user access." });
            }

            var businessId = _tenantService.CurrentBusinessId;

            // Check if target user is the owner — owners cannot have permissions modified
            var targetUserBusiness = await _membershipDbContext.UserBusinesses
                .FirstOrDefaultAsync(ub => ub.UserId == request.UserId && ub.BusinessId == businessId && ub.IsActive);

            if (targetUserBusiness == null)
            {
                return Json(new { success = false, message = "User not found in this business." });
            }

            if (targetUserBusiness.IsOwner)
            {
                return Json(new { success = false, message = "Cannot modify permissions for the business owner." });
            }

            // Find the permission record
            var permission = await _membershipDbContext.UserBusinessPermissions
                .FirstOrDefaultAsync(p => p.UserBusinessId == targetUserBusiness.Id && p.Module == request.Module);

            if (permission == null)
            {
                return Json(new { success = false, message = "Permission record not found." });
            }

            // Set access to none and deactivate
            permission.AccessLevel = AccessLevels.None;
            permission.IsActive = false;
            permission.DeactivatedAtUtc = DateTime.UtcNow;

            await _membershipDbContext.SaveChangesAsync();

            return Json(new { success = true, message = "Permission revoked successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred while revoking permission." });
        }
    }
}
