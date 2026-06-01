using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;
using Portal.Web.Models.Stripe;
using Portal.Web.Services.Stripe;

namespace Portal.Web.Controllers;

/// <summary>
/// Controller for the post-signup setup wizard where new business owners configure
/// their business details (name, VAT, address, logo, currency) before accessing the dashboard.
/// </summary>
[Authorize]
[Route("Setup")]
public class SetupWizardController : Controller
{
    private readonly ISetupWizardService _setupWizardService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ILogger<SetupWizardController> _logger;

    private static readonly HashSet<string> AllowedLogoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/svg+xml"
    };

    private static readonly HashSet<string> AllowedLogoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".svg"
    };

    private const long MaxLogoSizeBytes = 2 * 1024 * 1024; // 2MB

    public SetupWizardController(
        ISetupWizardService setupWizardService,
        ICurrentTenantService tenantService,
        ILogger<SetupWizardController> logger)
    {
        _setupWizardService = setupWizardService;
        _tenantService = tenantService;
        _logger = logger;
    }

    [HttpGet("Wizard")]
    public IActionResult Wizard()
    {
        var model = new SetupWizardModel();
        return View(model);
    }

    [HttpPost("Wizard")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Wizard(SetupWizardModel model)
    {
        var businessId = _tenantService.CurrentBusinessId;

        if (businessId == 0)
        {
            _logger.LogWarning("Setup wizard accessed by user with no BusinessId claim");
            return RedirectToAction("Index", "Home");
        }

        // Validate logo file if provided
        if (model.Logo is not null)
        {
            if (!ValidateLogoFile(model.Logo))
            {
                return View(model);
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await _setupWizardService.CompleteSetupAsync(businessId, model);

            if (!result.Success)
            {
                if (result.ValidationErrors.Count > 0)
                {
                    foreach (var error in result.ValidationErrors)
                    {
                        ModelState.AddModelError(error.Key, error.Value);
                    }
                }
                else if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    ModelState.AddModelError(string.Empty, result.ErrorMessage);
                }

                return View(model);
            }

            return Redirect("/");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing setup wizard for BusinessId {BusinessId}", businessId);
            ModelState.AddModelError(string.Empty, "An error occurred while saving your business details. Please try again.");
            return View(model);
        }
    }

    private bool ValidateLogoFile(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);

        if (!AllowedLogoExtensions.Contains(extension))
        {
            ModelState.AddModelError(nameof(SetupWizardModel.Logo),
                "Logo must be a PNG, JPG, or SVG file.");
            return false;
        }

        if (!AllowedLogoContentTypes.Contains(file.ContentType))
        {
            ModelState.AddModelError(nameof(SetupWizardModel.Logo),
                "Logo must be a PNG, JPG, or SVG file.");
            return false;
        }

        if (file.Length > MaxLogoSizeBytes)
        {
            ModelState.AddModelError(nameof(SetupWizardModel.Logo),
                "Logo file size must not exceed 2MB.");
            return false;
        }

        return true;
    }
}
