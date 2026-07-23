using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Repositories;
using Portal.Web.Models;
using Portal.Web.Models.PromoCode;
using Portal.Web.Services;
using Portal.Web.Services.Stripe;

namespace Portal.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRegistrationService _registrationService;
    private readonly IPlanService _planService;
    private readonly IIdentityEmailService _identityEmailService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IPlatformConfigService _platformConfigService;
    private readonly IProvisioningService _provisioningService;
    private readonly PromoCodeRepository _promoCodeRepository;
    private readonly IPromoCodeValidationService _promoCodeValidationService;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IRegistrationService registrationService,
        IPlanService planService,
        IIdentityEmailService identityEmailService,
        LinkGenerator linkGenerator,
        IPlatformConfigService platformConfigService,
        IProvisioningService provisioningService,
        PromoCodeRepository promoCodeRepository,
        IPromoCodeValidationService promoCodeValidationService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _registrationService = registrationService;
        _planService = planService;
        _identityEmailService = identityEmailService;
        _linkGenerator = linkGenerator;
        _platformConfigService = platformConfigService;
        _provisioningService = provisioningService;
        _promoCodeRepository = promoCodeRepository;
        _promoCodeValidationService = promoCodeValidationService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(string.Empty, "Email and password are required.");
            return View();
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View();
        }

        // Check if user has BusinessId or is SuperAdmin/DemoUser or has a pending registration (needs to complete checkout)
        var roles = await _userManager.GetRolesAsync(user);
        if (!user.BusinessId.HasValue && !roles.Contains("SuperAdmin") && !roles.Contains("DemoUser"))
        {
            // Allow login if user has a pending (incomplete) registration — they need to reach /Checkout
            var pendingRegistration = await _registrationService.GetPendingRegistrationByUserIdAsync(user.Id);
            if (pendingRegistration == null || pendingRegistration.IsCompleted)
            {
                ModelState.AddModelError(string.Empty, "Account not linked to a business.");
                return View();
            }
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            // If user has no business, redirect to checkout to complete payment
            if (!user.BusinessId.HasValue && !roles.Contains("SuperAdmin") && !roles.Contains("DemoUser"))
            {
                TempData["CheckoutMessage"] = "Your registration is incomplete. Please complete your subscription payment to activate your account.";
                return Redirect("/Checkout");
            }

            return LocalRedirect(returnUrl ?? "/");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
            return View();
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    /// <summary>
    /// Signs out and re-signs in the current user to refresh auth cookie claims (e.g., after provisioning sets BusinessId).
    /// Redirects to the dashboard after refresh.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> RefreshSession()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return RedirectToAction(nameof(Login));
        }

        // Sign out current session (stale claims)
        await _signInManager.SignOutAsync();

        // Re-sign in with fresh claims from the database
        await _signInManager.SignInAsync(user, isPersistent: false);

        return Redirect("/");
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Register(string? plan = null)
    {
        ViewData["Title"] = "Register";
        ViewData["Description"] = "Create your Portal account and select a subscription plan to get started.";
        ViewData["OgDescription"] = "Create your Portal account and select a subscription plan to get started.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/Register";

        var model = new RegisterViewModel();

        if (!string.IsNullOrWhiteSpace(plan))
        {
            var matchedPlan = await _planService.GetPlanBySlugAsync(plan);
            if (matchedPlan != null)
            {
                model.PreSelectedPlan = matchedPlan;
                model.SelectedPlanId = matchedPlan.Id;
            }
            else
            {
                model.AvailablePlans = await _planService.GetActivePlansOrderedAsync();
            }
        }
        else
        {
            model.AvailablePlans = await _planService.GetActivePlansOrderedAsync();
        }

        // Promo code field visibility logic
        var showPromoCodeConfig = await _platformConfigService.GetValueAsync("ShowPromoCodeField");
        var promoCodeQueryParam = Request.Query["promoCode"].ToString();
        var hasPromoCodeParam = !string.IsNullOrWhiteSpace(promoCodeQueryParam);

        // Show promo code field if config is "true" OR if promoCode query param is present
        var showPromoCodeField = string.Equals(showPromoCodeConfig, "true", StringComparison.OrdinalIgnoreCase) || hasPromoCodeParam;

        ViewBag.ShowPromoCodeField = showPromoCodeField;
        ViewBag.PrePopulatedPromoCode = hasPromoCodeParam ? promoCodeQueryParam : null;

        // Pre-populate the model if promoCode query param is present
        if (hasPromoCodeParam)
        {
            model.PromoCode = promoCodeQueryParam.Trim().ToUpperInvariant();
        }

        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        ViewData["Title"] = "Register";
        ViewData["Description"] = "Create your Portal account and select a subscription plan to get started.";
        ViewData["OgDescription"] = "Create your Portal account and select a subscription plan to get started.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/Register";

        if (!ModelState.IsValid)
        {
            await ReloadPlansForViewModel(model);
            await SetPromoCodeViewBag(model);
            return View(model);
        }

        // Promo code validation — server-side, before user creation (Req 5.4, 5.5, 9.2)
        if (!string.IsNullOrWhiteSpace(model.PromoCode))
        {
            var promoValidation = await _promoCodeValidationService.ValidateForRegistrationAsync(model.PromoCode, model.Email);

            if (!promoValidation.IsValid)
            {
                ModelState.AddModelError("PromoCode", promoValidation.ErrorMessage ?? "Invalid promo code.");
                await ReloadPlansForViewModel(model);
                await SetPromoCodeViewBag(model);
                return View(model);
            }

            // Store validated PromoCodeId for use by RegistrationService (Req 5.9)
            model.ValidatedPromoCodeId = promoValidation.PromoCodeId;
        }

        var result = await _registrationService.RegisterAsync(model);

        if (result.Succeeded)
        {
            return RedirectToAction(nameof(RegisterConfirmation));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        await ReloadPlansForViewModel(model);
        await SetPromoCodeViewBag(model);
        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostValidatePromoCode([FromForm] string code, [FromForm] string email)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Json(new { success = false, message = "Please enter a promo code." });

        var result = await _promoCodeValidationService.ValidateForRegistrationAsync(code, email ?? string.Empty);

        if (result.IsValid)
        {
            return Json(new
            {
                success = true,
                promoCodeId = result.PromoCodeId,
                durationMonths = result.DurationMonths,
                planId = result.PlanId,
                planName = result.PlanName
            });
        }

        return Json(new { success = false, message = result.ErrorMessage });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult RegisterConfirmation()
    {
        ViewData["Title"] = "Check Your Email";
        ViewData["Description"] = "A confirmation email has been sent. Please check your inbox to verify your account.";
        ViewData["OgDescription"] = "A confirmation email has been sent. Please check your inbox to verify your account.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/RegisterConfirmation";

        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? token)
    {
        ViewData["Title"] = "Confirm Account";
        ViewData["Description"] = "Confirm your Portal account email address to activate your account.";
        ViewData["OgDescription"] = "Confirm your Portal account email address to activate your account.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/ConfirmEmail";
        ViewData["NoIndex"] = true;

        // Missing parameters — show generic error
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            ViewBag.Status = "error";
            ViewBag.Message = "This verification link is invalid or has expired.";
            return View();
        }

        var user = await _userManager.FindByIdAsync(userId);

        // User not found — show same generic error (do not reveal existence)
        if (user == null)
        {
            ViewBag.Status = "error";
            ViewBag.Message = "This verification link is invalid or has expired.";
            return View();
        }

        // Email already confirmed — show "already verified" with Stripe CTA
        if (user.EmailConfirmed)
        {
            ViewBag.Status = "already-confirmed";
            ViewBag.Message = "Your email address has already been verified.";
            ViewBag.CheckoutUrl = await BuildCheckoutUrlAsync(userId);
            return View();
        }

        // Attempt to confirm the email
        var result = await _userManager.ConfirmEmailAsync(user, token);

        if (result.Succeeded)
        {
            // Load PendingRegistration to check for promo code path
            var pendingRegistration = await _registrationService.GetPendingRegistrationByUserIdAsync(userId);

            if (pendingRegistration?.PromoCodeId != null)
            {
                // Promo code user — provision trial without Stripe
                var promoCode = await _promoCodeRepository.GetByIdAsync(pendingRegistration.PromoCodeId.Value);

                if (promoCode != null)
                {
                    var promoRequest = new PromoProvisioningRequest
                    {
                        UserId = userId,
                        PendingRegistrationId = pendingRegistration.Id,
                        PlanId = pendingRegistration.PlanId,
                        PromoCodeId = promoCode.Id,
                        DurationMonths = promoCode.DurationMonths
                    };

                    var provisioningResult = await _provisioningService.ProvisionPromoTrialAsync(promoRequest);

                    if (provisioningResult.Success)
                    {
                        ViewBag.Status = "promo-success";
                        ViewBag.Message = "Your email address has been confirmed and your trial has been activated.";
                        ViewBag.RedirectUrl = "/SetupWizard";
                        return View();
                    }
                    else
                    {
                        ViewBag.Status = "promo-failed";
                        ViewBag.Message = "Your email has been confirmed, but your promo code is no longer valid.";
                        ViewBag.RedirectUrl = "/Account/PromoCodeExpired";
                        return View();
                    }
                }
            }

            // Standard Stripe checkout flow — auto-sign-in and redirect directly to checkout
            await _signInManager.SignInAsync(user, isPersistent: false);
            return Redirect("/Checkout");
        }

        // Invalid or expired token
        ViewBag.Status = "error";
        ViewBag.Message = "This verification link is invalid or has expired.";
        return View();
    }

    private async Task<string> BuildCheckoutUrlAsync(string userId)
    {
        var pendingRegistration = await _registrationService.GetPendingRegistrationByUserIdAsync(userId);
        if (pendingRegistration != null)
        {
            return "/Checkout";
        }

        return "/Checkout";
    }

    private async Task ReloadPlansForViewModel(RegisterViewModel model)
    {
        if (model.PreSelectedPlan == null)
        {
            model.AvailablePlans = await _planService.GetActivePlansOrderedAsync();
        }
    }

    /// <summary>
    /// Sets ViewBag properties for promo code field visibility on POST validation failures.
    /// Shows the field if config is "true" OR if the user submitted a promo code value.
    /// </summary>
    private async Task SetPromoCodeViewBag(RegisterViewModel model)
    {
        var showPromoCodeConfig = await _platformConfigService.GetValueAsync("ShowPromoCodeField");
        var hasPromoCode = !string.IsNullOrWhiteSpace(model.PromoCode);

        ViewBag.ShowPromoCodeField = string.Equals(showPromoCodeConfig, "true", StringComparison.OrdinalIgnoreCase) || hasPromoCode;
        ViewBag.PrePopulatedPromoCode = hasPromoCode ? model.PromoCode : null;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        ViewData["Title"] = "Forgot Password";
        ViewData["Description"] = "Request a password reset link for your Portal account.";
        ViewData["OgDescription"] = "Request a password reset link for your Portal account.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/ForgotPassword";

        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        ViewData["Title"] = "Forgot Password";
        ViewData["Description"] = "Request a password reset link for your Portal account.";
        ViewData["OgDescription"] = "Request a password reset link for your Portal account.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/ForgotPassword";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Always redirect to confirmation regardless of email existence (prevents enumeration)
        var user = await _userManager.FindByEmailAsync(model.Email);

        // Only generate token and send email if user exists AND email is confirmed
        if (user != null && user.EmailConfirmed)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var resetLink = _linkGenerator.GetUriByAction(
                HttpContext,
                action: "ResetPassword",
                controller: "Account",
                values: new { userId = user.Id, token });

            if (!string.IsNullOrEmpty(resetLink))
            {
                await _identityEmailService.SendPasswordResetAsync(model.Email, resetLink);
            }
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation()
    {
        ViewData["Title"] = "Password Reset Requested";
        ViewData["Description"] = "If an account exists with that email, a reset link has been sent.";
        ViewData["OgDescription"] = "If an account exists with that email, a reset link has been sent.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/ForgotPasswordConfirmation";

        return View();
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // ─── Reset Password ───────────────────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(string? userId, string? token)
    {
        ViewData["Title"] = "Reset Password";
        ViewData["Description"] = "Set a new password for your Portal account using your secure reset link.";
        ViewData["OgDescription"] = "Set a new password for your Portal account using your secure reset link.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword";
        ViewData["NoIndex"] = true;

        // Validate parameters are present
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            ViewData["Error"] = "This password reset link is invalid. Please request a new reset link.";
            return View();
        }

        // Find user — generic error if not found (never reveal user existence)
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            ViewData["Error"] = "This password reset link is invalid. Please request a new reset link.";
            return View();
        }

        // Validate token before showing the form
        var isValidToken = await _userManager.VerifyUserTokenAsync(
            user,
            TokenOptions.DefaultProvider,
            "ResetPassword",
            token);

        if (!isValidToken)
        {
            ViewData["Error"] = "This password reset link is invalid or has expired. Please request a new reset link.";
            return View();
        }

        // Token is valid — show the password form
        var model = new ResetPasswordViewModel
        {
            UserId = userId,
            Token = token
        };

        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        ViewData["Title"] = "Reset Password";
        ViewData["Description"] = "Set a new password for your Portal account using your secure reset link.";
        ViewData["OgDescription"] = "Set a new password for your Portal account using your secure reset link.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword";
        ViewData["NoIndex"] = true;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Find user — generic error if not found (never reveal user existence)
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            ViewData["Error"] = "This password reset link is invalid. Please request a new reset link.";
            return View();
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

        if (result.Succeeded)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        // Map IdentityErrors to ModelState
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPasswordConfirmation()
    {
        ViewData["Title"] = "Password Reset Complete";
        ViewData["Description"] = "Your password has been successfully reset. You can now log in with your new password.";
        ViewData["OgDescription"] = "Your password has been successfully reset. You can now log in with your new password.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/ResetPasswordConfirmation";

        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult PromoCodeExpired()
    {
        ViewData["Title"] = "Promo Code No Longer Valid";
        ViewData["Description"] = "Your promotional code is no longer valid. Subscribe to continue using the platform.";
        ViewData["OgDescription"] = "Your promotional code is no longer valid. Subscribe to continue using the platform.";
        ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/Account/PromoCodeExpired";
        ViewData["NoIndex"] = true;

        return View();
    }
}
