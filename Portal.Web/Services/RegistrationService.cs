using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Repositories;
using Portal.Web.Models;

namespace Portal.Web.Services;

/// <summary>
/// Handles public self-service registration: creating users, tracking pending registrations,
/// and managing the flow between registration and email confirmation.
/// </summary>
public class RegistrationService : IRegistrationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MembershipDbContext _membershipDbContext;
    private readonly PortalDbContext _portalDbContext;
    private readonly IIdentityEmailService _identityEmailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly IPlanRepository _planRepository;
    private readonly PromoCodeRepository _promoCodeRepository;
    private readonly DemoInvitationRepository _demoInvitationRepository;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(
        UserManager<ApplicationUser> userManager,
        MembershipDbContext membershipDbContext,
        PortalDbContext portalDbContext,
        IIdentityEmailService identityEmailService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IPlanRepository planRepository,
        PromoCodeRepository promoCodeRepository,
        DemoInvitationRepository demoInvitationRepository,
        ILogger<RegistrationService> logger)
    {
        _userManager = userManager;
        _membershipDbContext = membershipDbContext;
        _portalDbContext = portalDbContext;
        _identityEmailService = identityEmailService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _planRepository = planRepository;
        _promoCodeRepository = promoCodeRepository;
        _demoInvitationRepository = demoInvitationRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RegistrationResult> RegisterAsync(RegisterViewModel model)
    {
        try
        {
            // Check for existing user with this email
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            ApplicationUser user;

            if (existingUser != null)
            {
                // Check if this is a demo-only user (can be converted)
                var isDemoOnly = await IsDemoOnlyUserAsync(existingUser.Id);

                if (!isDemoOnly)
                {
                    return RegistrationResult.Failure("An account with this email address already exists.");
                }

                // Convert demo user: reset password, update profile, force email confirmation
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
                var resetResult = await _userManager.ResetPasswordAsync(existingUser, resetToken, model.Password);
                if (!resetResult.Succeeded)
                {
                    var errors = resetResult.Errors.Select(e => e.Description);
                    return RegistrationResult.Failure(errors);
                }

                existingUser.FirstName = model.FirstName;
                existingUser.LastName = model.LastName;
                existingUser.EmailConfirmed = false;
                await _userManager.UpdateAsync(existingUser);

                user = existingUser;
                _logger.LogInformation("Demo user {UserId} converted for real registration with email {Email}", user.Id, model.Email);

                // Stamp the demo invitation as converted (non-blocking)
                try { await _demoInvitationRepository.MarkConvertedByEmailAsync(model.Email); }
                catch { /* Non-blocking — don't fail registration if this fails */ }
            }
            else
            {
                // Normal path: create new user
                user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    EmailConfirmed = false,
                    BusinessId = null,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var identityResult = await _userManager.CreateAsync(user, model.Password);
                if (!identityResult.Succeeded)
                {
                    var errors = identityResult.Errors.Select(e => e.Description);
                    return RegistrationResult.Failure(errors);
                }
            }

            // Determine PlanId — promo code takes precedence, then user selection
            int planId;
            if (model.ValidatedPromoCodeId.HasValue)
            {
                var promoCode = await _promoCodeRepository.GetByIdAsync(model.ValidatedPromoCodeId.Value);
                if (promoCode?.PlanId != null)
                {
                    planId = promoCode.PlanId.Value;
                }
                else
                {
                    // Fallback: legacy codes without PlanId → Professional tier
                    var professionalPlan = await _planRepository.GetBySlugAsync("professional");
                    if (professionalPlan == null)
                    {
                        _logger.LogError("Professional plan not found in database. Cannot complete promo code registration for user {UserId}", user.Id);
                        return RegistrationResult.Failure("Registration could not be completed. Please try again later.");
                    }
                    planId = professionalPlan.Id;
                }
            }
            else if (model.SelectedPlanId.HasValue)
            {
                planId = model.SelectedPlanId.Value;
            }
            else
            {
                return RegistrationResult.Failure("Please select a subscription plan.");
            }

            // Create PendingRegistration record with resolved PlanId
            var pendingRegistration = new PendingRegistration
            {
                UserId = user.Id,
                PlanId = planId,
                PromoCodeId = model.ValidatedPromoCodeId,
                IsCompleted = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            _membershipDbContext.PendingRegistrations.Add(pendingRegistration);
            await _membershipDbContext.SaveChangesAsync();

            // Generate email confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            // Build confirmation link
            var confirmationLink = GenerateConfirmationLink(user.Id, token);

            // Send confirmation email via IIdentityEmailService
            await _identityEmailService.SendEmailConfirmationAsync(model.Email, confirmationLink);

            _logger.LogInformation("User {UserId} registered successfully with email {Email}", user.Id, model.Email);

            return RegistrationResult.Success(user.Id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PendingRegistration?> GetPendingRegistrationByUserIdAsync(string userId)
    {
        try
        {
            return await _membershipDbContext.PendingRegistrations
                .FirstOrDefaultAsync(pr => pr.UserId == userId);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task MarkPendingRegistrationCompletedAsync(string userId)
    {
        try
        {
            var pendingRegistration = await _membershipDbContext.PendingRegistrations
                .FirstOrDefaultAsync(pr => pr.UserId == userId);

            if (pendingRegistration != null)
            {
                pendingRegistration.IsCompleted = true;
                pendingRegistration.CompletedAtUtc = DateTime.UtcNow;
                await _membershipDbContext.SaveChangesAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    private string GenerateConfirmationLink(string userId, string token)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext is null when generating confirmation link for user {UserId}", userId);
            return string.Empty;
        }

        var confirmationLink = _linkGenerator.GetUriByAction(
            httpContext,
            action: "ConfirmEmail",
            controller: "Account",
            values: new { userId, token });

        return confirmationLink ?? string.Empty;
    }

    /// <summary>
    /// Checks if a user is linked only to demo businesses (IsDemoAccount = true).
    /// Returns true if the user has no businesses or all linked businesses are demos.
    /// </summary>
    private async Task<bool> IsDemoOnlyUserAsync(string userId)
    {
        var businessIds = await _membershipDbContext.UserBusinesses
            .Where(ub => ub.UserId == userId && ub.IsActive)
            .Select(ub => ub.BusinessId)
            .ToListAsync();

        if (!businessIds.Any())
            return true;

        // Verify the businesses actually exist in Portal DB and are all demos
        var matchingBusinesses = await _portalDbContext.Businesses
            .Where(b => businessIds.Contains(b.Id))
            .ToListAsync();

        // If no matching businesses found in Portal DB, something is wrong — don't allow conversion
        if (!matchingBusinesses.Any())
            return false;

        return matchingBusinesses.All(b => b.IsDemoAccount);
    }
}
