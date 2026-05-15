using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

public class InvitationController : Controller
{
    private readonly IInvitationService _invitationService;
    private readonly IEmailService _emailService;
    private readonly IBusinessService _businessService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public InvitationController(
        IInvitationService invitationService,
        IEmailService emailService,
        IBusinessService businessService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _invitationService = invitationService;
        _emailService = emailService;
        _businessService = businessService;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var businesses = await _businessService.GetAllBusinessesAsync();
        var invitations = await _invitationService.GetAllInvitationsAsync();
        ViewBag.Businesses = businesses;
        ViewBag.Invitations = invitations;
        return View();
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email, int businessId, List<InvitationModulePermission>? modulePermissions)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(string.Empty, "Email is required.");
            var businesses = await _businessService.GetAllBusinessesAsync();
            var invitations = await _invitationService.GetAllInvitationsAsync();
            ViewBag.Businesses = businesses;
            ViewBag.Invitations = invitations;
            return View();
        }

        if (modulePermissions != null)
        {
            foreach (var perm in modulePermissions)
            {
                if (!PortalModules.IsValid(perm.Module))
                {
                    ModelState.AddModelError(string.Empty, $"Invalid module: {perm.Module}");
                }
                if (!AccessLevels.IsValid(perm.AccessLevel))
                {
                    ModelState.AddModelError(string.Empty, $"Invalid access level: {perm.AccessLevel}");
                }
            }

            if (!ModelState.IsValid)
            {
                var businesses = await _businessService.GetAllBusinessesAsync();
                var invitations = await _invitationService.GetAllInvitationsAsync();
                ViewBag.Businesses = businesses;
                ViewBag.Invitations = invitations;
                return View();
            }
        }

        var userId = _userManager.GetUserId(User)!;
        var invitation = await _invitationService.CreateInvitationAsync(email, businessId, userId, modulePermissions);

        var registrationLink = Url.Action("Register", "Invitation", new { token = invitation.Token }, Request.Scheme)!;
        var business = await _businessService.GetBusinessByIdAsync(businessId);
        await _emailService.SendInvitationEmailAsync(email, registrationLink, business?.Name ?? "Unknown");

        TempData["SuccessMessage"] = $"Invitation sent to {email}";
        return RedirectToAction(nameof(Create));
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        await _invitationService.CancelInvitationAsync(id);
        TempData["SuccessMessage"] = "Invitation cancelled.";
        return RedirectToAction(nameof(Create));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Register(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ViewBag.Error = "Invalid invitation link.";
            return View("RegisterError");
        }

        var invitation = await _invitationService.ValidateTokenAsync(token);
        if (invitation == null)
        {
            ViewBag.Error = "This invitation is invalid or has expired.";
            return View("RegisterError");
        }

        ViewBag.Email = invitation.Email;
        ViewBag.Token = token;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string token, string firstName, string lastName, string password)
    {
        var invitation = await _invitationService.ValidateTokenAsync(token);
        if (invitation == null)
        {
            ViewBag.Error = "This invitation is invalid or has expired.";
            return View("RegisterError");
        }

        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(invitation.Email);
        if (existingUser != null)
        {
            ViewBag.Error = "Account already exists.";
            return View("RegisterError");
        }

        var user = new ApplicationUser
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            BusinessId = invitation.BusinessId,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            ViewBag.Email = invitation.Email;
            ViewBag.Token = token;
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View();
        }

        await _invitationService.CreateUserBusinessFromInvitationAsync(user.Id, invitation);
        await _invitationService.MarkAsUsedAsync(invitation.Id);
        await _signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToAction("Index", "Home");
    }
}
