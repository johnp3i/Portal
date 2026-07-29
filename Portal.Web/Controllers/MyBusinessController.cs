using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Services.Stripe;
using Serilog;
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
    private readonly IPaymentInstructionsService _paymentInstructionsService;
    private readonly PortalDbContext _dbContext;
    private readonly IStripeConnectService _stripeConnectService;
    private readonly BusinessApiKeysRepository _businessApiKeysRepository;
    private readonly IStripeKeyEncryptionService _stripeKeyEncryptionService;

    public MyBusinessController(IBusinessService businessService, ILogoService logoService,
        ICurrentTenantService tenantService, BusinessPaymentDetailRepository paymentDetailRepository,
        IPlanCheckService planCheckService, MembershipDbContext membershipDbContext,
        IPaymentInstructionsService paymentInstructionsService, PortalDbContext dbContext,
        IStripeConnectService stripeConnectService, BusinessApiKeysRepository businessApiKeysRepository,
        IStripeKeyEncryptionService stripeKeyEncryptionService)
    {
        _businessService = businessService;
        _logoService = logoService;
        _tenantService = tenantService;
        _paymentDetailRepository = paymentDetailRepository;
        _planCheckService = planCheckService;
        _membershipDbContext = membershipDbContext;
        _paymentInstructionsService = paymentInstructionsService;
        _dbContext = dbContext;
        _stripeConnectService = stripeConnectService;
        _businessApiKeysRepository = businessApiKeysRepository;
        _stripeKeyEncryptionService = stripeKeyEncryptionService;
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
        ViewBag.IsPaymentInstructionsEnabled = business?.IsPaymentInstructionsEnabled ?? false;
        ViewBag.IsAutoReceiptEnabled = business?.IsAutoReceiptEnabled ?? false;
        ViewBag.IsAutoInvoiceSignatureEnabled = business?.IsAutoInvoiceSignatureEnabled ?? false;

        // Z-Report feature flag
        ViewBag.IsZReportEnabled = profile?.IsZReportEnabled ?? false;

        // Stripe Connect status
        ViewBag.IsStripeConnected = await _stripeConnectService.IsConnectedAsync(businessId);

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
    public async Task<IActionResult> AddPaymentDetail(string label, string bankName, string iban, string payeeName, string? swiftBic)
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
            SwiftBic = swiftBic?.Trim(),
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
    public async Task<IActionResult> UpdatePaymentDetail(int id, string label, string bankName, string iban, string payeeName, string? swiftBic)
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
            label.Trim(), bankName.Trim(), iban.Trim(), payeeName.Trim(), swiftBic?.Trim());
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostTogglePaymentInstructions(bool enabled)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _paymentInstructionsService.SetPaymentInstructionsEnabledAsync(businessId, enabled);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update payment instructions setting." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleAutoReceipt(bool enabled)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE [portal].[Business] SET [IsAutoReceiptEnabled] = @Enabled WHERE [Id] = @Id",
                new Microsoft.Data.SqlClient.SqlParameter("@Enabled", enabled),
                new Microsoft.Data.SqlClient.SqlParameter("@Id", businessId));
            return Json(new { success = true, message = enabled ? "Auto-receipt enabled." : "Auto-receipt disabled." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update auto-receipt setting." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleAutoInvoiceSignature(bool enabled)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE [portal].[Business] SET [IsAutoInvoiceSignatureEnabled] = @Enabled WHERE [Id] = @Id",
                new Microsoft.Data.SqlClient.SqlParameter("@Enabled", enabled),
                new Microsoft.Data.SqlClient.SqlParameter("@Id", businessId));
            return Json(new { success = true, message = enabled ? "Auto-signature on invoices enabled." : "Auto-signature on invoices disabled." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update setting." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleZReport(bool enabled)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var profile = await _businessService.GetBusinessProfileAsync(businessId);
            if (profile == null)
                return Json(new { success = false, message = "Business profile not found." });

            profile.IsZReportEnabled = enabled;
            await _businessService.SaveBusinessProfileAsync(profile);

            return Json(new { success = true, message = enabled ? "Z-Report entry enabled. You can now record POS revenue." : "Z-Report entry disabled." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update Z-Report setting." });
        }
    }

    // ═══ STRIPE CONNECT ═══════════════════════════════════════════════════

    // ─── Stripe API Key Management ───────────────────────────────────────

    /// <summary>
    /// Returns masked key status for display. Owners see masked values; team members see only whether keys are configured.
    /// GET /MyBusiness/AxGetStripeKeyStatus
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AxGetStripeKeyStatus()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var isOwner = User.HasClaim("IsOwner", "true");

            var keys = await _businessApiKeysRepository.GetByBusinessIdAsync(businessId);
            var isConfigured = keys.Count > 0;

            if (!isOwner)
            {
                return Json(new { success = true, isConfigured, isOwner = false });
            }

            // Owner: return masked values
            string? connectClientIdMasked = null;
            string? secretKeyMasked = null;
            string? webhookSecretMasked = null;

            foreach (var key in keys)
            {
                var decrypted = _stripeKeyEncryptionService.Decrypt(key.EncryptedValue);
                var masked = _stripeKeyEncryptionService.Mask(decrypted);
                switch (key.KeyType)
                {
                    case "connect_client_id": connectClientIdMasked = masked; break;
                    case "secret_key": secretKeyMasked = masked; break;
                    case "webhook_secret": webhookSecretMasked = masked; break;
                }
            }

            return Json(new { success = true, isConfigured, isOwner = true, connectClientIdMasked, secretKeyMasked, webhookSecretMasked });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load key status." });
        }
    }

    /// <summary>
    /// Validates and saves Stripe API keys. Validates the Secret Key by calling Stripe before persisting.
    /// POST /MyBusiness/AxPostSaveStripeKeys
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSaveStripeKeys([FromBody] SaveStripeKeysRequest request)
    {
        try
        {
            if (!CanEdit())
                return Json(new { success = false, message = "Only the business owner can manage API keys." });

            if (string.IsNullOrWhiteSpace(request?.SecretKey))
                return Json(new { success = false, message = "Secret Key is required." });

            // Validate the secret key by calling Stripe
            try
            {
                var accountService = new Stripe.AccountService(new Stripe.StripeClient(request.SecretKey));
                await accountService.GetSelfAsync();
            }
            catch (Stripe.StripeException ex)
            {
                return Json(new { success = false, message = "Invalid Secret Key. Stripe returned: " + ex.Message });
            }
            catch (HttpRequestException)
            {
                return Json(new { success = false, message = "Could not reach Stripe to validate keys. Please try again." });
            }

            var businessId = _tenantService.CurrentBusinessId;

            // Encrypt and save each key
            if (!string.IsNullOrWhiteSpace(request.ConnectClientId))
            {
                await _businessApiKeysRepository.UpsertAsync(new BusinessApiKey
                {
                    BusinessId = businessId,
                    KeyType = "connect_client_id",
                    EncryptedValue = _stripeKeyEncryptionService.Encrypt(request.ConnectClientId.Trim())
                });
            }

            await _businessApiKeysRepository.UpsertAsync(new BusinessApiKey
            {
                BusinessId = businessId,
                KeyType = "secret_key",
                EncryptedValue = _stripeKeyEncryptionService.Encrypt(request.SecretKey.Trim())
            });

            if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
            {
                await _businessApiKeysRepository.UpsertAsync(new BusinessApiKey
                {
                    BusinessId = businessId,
                    KeyType = "webhook_secret",
                    EncryptedValue = _stripeKeyEncryptionService.Encrypt(request.WebhookSecret.Trim())
                });
            }

            Log.Information("Stripe API keys saved for business {BusinessId} by user {UserId}", businessId, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            return Json(new { success = true, message = "Stripe API keys saved and validated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to save keys. Please try again." });
        }
    }

    /// <summary>
    /// Reveals the full decrypted value of a specific key type. Rate-limited to 10 requests/minute/user.
    /// POST /MyBusiness/AxPostRevealStripeKey
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostRevealStripeKey([FromBody] RevealStripeKeyRequest request)
    {
        try
        {
            if (!CanEdit())
                return Json(new { success = false, message = "Only the business owner can reveal API keys." });

            if (string.IsNullOrWhiteSpace(request?.KeyType) || !StripeKeyTypes.IsValid(request.KeyType))
                return Json(new { success = false, message = "Invalid key type." });

            // Rate limiting (10 per minute) using IMemoryCache
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var rateLimitKey = $"stripe_reveal_{userId}_{DateTime.UtcNow:yyyyMMddHHmm}";
            var cache = HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var count = cache.GetOrCreate(rateLimitKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return 0;
            });
            if (count >= 10)
                return StatusCode(429, new { success = false, message = "Too many reveal requests. Please wait a minute." });
            cache.Set(rateLimitKey, count + 1, TimeSpan.FromMinutes(1));

            var businessId = _tenantService.CurrentBusinessId;
            var key = await _businessApiKeysRepository.GetByBusinessIdAndKeyTypeAsync(businessId, request.KeyType);
            if (key == null)
                return Json(new { success = false, message = "Key not found." });

            var decrypted = _stripeKeyEncryptionService.Decrypt(key.EncryptedValue);

            Log.Information("Stripe API key revealed: {KeyType} for business {BusinessId} by user {UserId}", request.KeyType, businessId, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            return Json(new { success = true, value = decrypted });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to reveal key." });
        }
    }

    /// <summary>
    /// Removes all per-business Stripe API keys. Platform defaults will be used if available.
    /// POST /MyBusiness/AxPostDeleteStripeKeys
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeleteStripeKeys()
    {
        try
        {
            if (!CanEdit())
                return Json(new { success = false, message = "Only the business owner can manage API keys." });

            var businessId = _tenantService.CurrentBusinessId;
            await _businessApiKeysRepository.DeleteAllByBusinessIdAsync(businessId);

            Log.Information("Stripe API keys deleted for business {BusinessId} by user {UserId}", businessId, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            return Json(new { success = true, message = "API keys removed. Platform defaults will be used if available." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to remove keys." });
        }
    }

    // ─── Stripe Connect OAuth ────────────────────────────────────────────

    /// <summary>
    /// Redirects the business owner to Stripe's OAuth page to connect their account.
    /// GET /MyBusiness/StripeConnect
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> StripeConnect()
    {
        if (!CanEdit())
            return Forbid();

        var businessId = _tenantService.CurrentBusinessId;
        var oauthUrl = await _stripeConnectService.GetOAuthConnectUrlAsync(businessId);
        return Redirect(oauthUrl);
    }

    /// <summary>
    /// OAuth callback from Stripe after user authorizes the connection.
    /// GET /MyBusiness/StripeConnectCallback?code=xxx&state=xxx
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> StripeConnectCallback(string? code, string? state, string? error, string? error_description)
    {
        if (!CanEdit())
            return Forbid();

        // Handle user denying access
        if (!string.IsNullOrEmpty(error))
        {
            TempData["StripeConnectError"] = error_description ?? "Stripe connection was cancelled.";
            return RedirectToAction("Index");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            TempData["StripeConnectError"] = "Invalid callback parameters.";
            return RedirectToAction("Index");
        }

        var businessId = _tenantService.CurrentBusinessId;
        var result = await _stripeConnectService.CompleteOAuthAsync(businessId, code, state);

        if (result.Success)
        {
            TempData["StripeConnectSuccess"] = "Stripe account connected successfully! You can now accept card payments.";
        }
        else
        {
            TempData["StripeConnectError"] = result.Message;
        }

        return RedirectToAction("Index");
    }

    /// <summary>
    /// Disconnects the business from Stripe Connect.
    /// POST /MyBusiness/AxPostDisconnectStripe
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDisconnectStripe()
    {
        try
        {
            if (!CanEdit())
                return Json(new { success = false, message = "Only the business owner can manage Stripe connection." });

            var businessId = _tenantService.CurrentBusinessId;
            var result = await _stripeConnectService.DisconnectAsync(businessId);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to disconnect Stripe. Please try again." });
        }
    }
}
