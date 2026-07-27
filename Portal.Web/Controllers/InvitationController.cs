using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

public class InvitationController : Controller
{
    private readonly IInvitationService _invitationService;
    private readonly IEmailService _emailService;
    private readonly IBusinessService _businessService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IBusinessPlanRepository _businessPlanRepository;
    private readonly IPlanRepository _planRepository;
    private readonly MembershipDbContext _membershipDbContext;

    public InvitationController(
        IInvitationService invitationService,
        IEmailService emailService,
        IBusinessService businessService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IBusinessPlanRepository businessPlanRepository,
        IPlanRepository planRepository,
        MembershipDbContext membershipDbContext)
    {
        _invitationService = invitationService;
        _emailService = emailService;
        _businessService = businessService;
        _userManager = userManager;
        _signInManager = signInManager;
        _businessPlanRepository = businessPlanRepository;
        _planRepository = planRepository;
        _membershipDbContext = membershipDbContext;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var isSuperAdmin = User.IsInRole("SuperAdmin");

        if (isSuperAdmin)
        {
            var businesses = await _businessService.GetAllBusinessesAsync();
            ViewBag.Businesses = businesses;
        }
        else
        {
            // Regular owner — can only invite to their own business
            var businessIdClaim = User.FindFirst("BusinessId");
            if (businessIdClaim == null || !int.TryParse(businessIdClaim.Value, out var businessId))
                return Forbid();

            var business = await _businessService.GetBusinessByIdAsync(businessId);
            ViewBag.Businesses = business != null ? new List<Portal.Infrastructure.Entities.Business> { business } : new List<Portal.Infrastructure.Entities.Business>();
        }

        List<Portal.Infrastructure.Entities.Identity.Invitation> invitations;
        if (isSuperAdmin)
        {
            invitations = await _invitationService.GetAllInvitationsAsync();
        }
        else
        {
            var allInvitations = await _invitationService.GetAllInvitationsAsync();
            var bizIdForFilter = User.FindFirst("BusinessId");
            if (bizIdForFilter != null && int.TryParse(bizIdForFilter.Value, out var filterBizId))
            {
                invitations = allInvitations.Where(i => i.BusinessId == filterBizId).ToList();
            }
            else
            {
                invitations = new List<Portal.Infrastructure.Entities.Identity.Invitation>();
            }
        }
        ViewBag.Invitations = invitations;

        // Seat usage info
        var bizIdClaim = User.FindFirst("BusinessId");
        if (bizIdClaim != null && int.TryParse(bizIdClaim.Value, out var bizId))
        {
            var activePlan = await _businessPlanRepository.GetActiveByBusinessIdAsync(bizId);
            if (activePlan != null)
            {
                var plan = await _planRepository.GetByIdAsync(activePlan.PlanId);
                var maxUsers = plan?.MaxUsers ?? -1;
                var activeUsers = await _membershipDbContext.UserBusinesses
                    .CountAsync(ub => ub.BusinessId == bizId && ub.IsActive);
                var pendingInvitations = invitations?.Count(i => i.BusinessId == bizId && !i.IsUsed && i.ExpiresAtUtc > DateTime.UtcNow) ?? 0;

                ViewBag.MaxUsers = maxUsers;
                ViewBag.ActiveUsers = activeUsers;
                ViewBag.PendingInvitations = pendingInvitations;
                ViewBag.SeatsRemaining = maxUsers == -1 ? -1 : maxUsers - (activeUsers + pendingInvitations);
            }
        }

        return View();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email, int businessId, List<InvitationModulePermission>? modulePermissions)
    {
        // Security: non-SuperAdmin can only invite to their own business
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        if (!isSuperAdmin)
        {
            var userBizClaim = User.FindFirst("BusinessId");
            if (userBizClaim == null || !int.TryParse(userBizClaim.Value, out var userBizId) || userBizId != businessId)
                return Forbid();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(string.Empty, "Email is required.");
            return RedirectToAction(nameof(Create));
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
                return RedirectToAction(nameof(Create));
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

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        // Security: validate the invitation belongs to the user's business
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        if (!isSuperAdmin)
        {
            var allInvitations = await _invitationService.GetAllInvitationsAsync();
            var invitation = allInvitations.FirstOrDefault(i => i.Id == id);
            var userBizClaim = User.FindFirst("BusinessId");
            if (invitation == null || userBizClaim == null || !int.TryParse(userBizClaim.Value, out var userBizId) || invitation.BusinessId != userBizId)
                return Forbid();
        }

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
