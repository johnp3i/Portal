using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

/// <summary>
/// Public (unauthenticated) controller handling demo magic link entry.
/// Validates the invitation token, creates or retrieves a demo user,
/// signs them in with demo-specific claims, and redirects to the dashboard.
/// </summary>
[AllowAnonymous]
public class DemoController : Controller
{
    private readonly IDemoInvitationService _demoInvitationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<DemoController> _logger;

    public DemoController(
        IDemoInvitationService demoInvitationService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<DemoController> logger)
    {
        _demoInvitationService = demoInvitationService;
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <summary>
    /// Demo entry endpoint — validates the magic link token, creates/retrieves
    /// the demo user, signs them in with demo claims, and redirects to dashboard.
    /// </summary>
    [HttpGet("Demo/Enter")]
    public async Task<IActionResult> Enter(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Demo entry attempted with missing or empty token");
            return View("DemoInvalid");
        }

        var result = await _demoInvitationService.ValidateAndTrackAccessAsync(token);

        if (!result.IsValid)
        {
            _logger.LogWarning("Demo entry failed for token. Reason: {ErrorReason}", result.ErrorReason);

            return result.ErrorReason switch
            {
                "expired" => View("DemoExpired"),
                "revoked" => View("DemoRevoked"),
                _ => View("DemoInvalid")
            };
        }

        // Sign out any existing session before creating the demo session
        await _signInManager.SignOutAsync();

        var invitation = result.Invitation!;

        // Create or retrieve demo user for this recipient + business
        var user = await _userManager.FindByEmailAsync(invitation.RecipientEmail);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = invitation.RecipientEmail,
                Email = invitation.RecipientEmail,
                EmailConfirmed = true,
                FirstName = invitation.RecipientName ?? "Demo",
                LastName = "User",
                BusinessId = invitation.BusinessId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, GenerateRandomPassword());
            if (!createResult.Succeeded)
            {
                _logger.LogError("Failed to create demo user for {Email}. Errors: {Errors}",
                    invitation.RecipientEmail,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return View("DemoInvalid");
            }

            // Assign DemoUser role
            await _userManager.AddToRoleAsync(user, "DemoUser");

            _logger.LogInformation("Created demo user {UserId} for {Email}",
                user.Id, invitation.RecipientEmail);
        }

        // Ensure UserBusiness mapping exists for this demo user + business
        await _demoInvitationService.EnsureDemoUserBusinessAsync(user.Id, invitation.BusinessId);

        // Ensure DemoUser role exists (for newly created users)
        if (!await _roleManager.RoleExistsAsync("DemoUser"))
        {
            await _roleManager.CreateAsync(new IdentityRole("DemoUser"));
        }

        // Load permissions and serialize into claims for in-session enforcement (no DB calls per request)
        var permissionsDict = await _demoInvitationService.GetPermissionsForInvitationAsync(invitation.Id);
        var permissionsJson = System.Text.Json.JsonSerializer.Serialize(permissionsDict);

        // Sign in with demo-specific claims
        var additionalClaims = new List<Claim>
        {
            new Claim("DemoInvitationId", invitation.Id.ToString()),
            new Claim("BusinessId", invitation.BusinessId.ToString()),
            new Claim("IsDemoSession", "true"),
            new Claim("DemoPermissions", permissionsJson),
            new Claim("DemoInvitationExpiresAtUtc", invitation.ExpiresAtUtc.ToString("O"))
        };

        await _signInManager.SignInWithClaimsAsync(user, new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2),
            AllowRefresh = true
        }, additionalClaims);

        _logger.LogInformation("Demo session started for {Email} on business {BusinessId} via invitation {InvitationId}",
            invitation.RecipientEmail, invitation.BusinessId, invitation.Id);

        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Generates a cryptographically secure random password that meets Identity requirements.
    /// Demo users never log in with a password — this is for record creation only.
    /// </summary>
    private static string GenerateRandomPassword()
    {
        const int length = 32;
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%^&*";
        const string all = upper + lower + digits + special;

        var password = new char[length];
        var randomBytes = new byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        // Ensure at least one of each required character type
        password[0] = upper[randomBytes[0] % upper.Length];
        password[1] = lower[randomBytes[1] % lower.Length];
        password[2] = digits[randomBytes[2] % digits.Length];
        password[3] = special[randomBytes[3] % special.Length];

        // Fill the rest with random characters from the full set
        for (int i = 4; i < length; i++)
        {
            password[i] = all[randomBytes[i] % all.Length];
        }

        // Shuffle the password to avoid predictable prefix
        for (int i = password.Length - 1; i > 0; i--)
        {
            int j = randomBytes[i] % (i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }
}
