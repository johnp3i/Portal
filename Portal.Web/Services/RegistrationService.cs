using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Identity;
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
    private readonly IIdentityEmailService _identityEmailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(
        UserManager<ApplicationUser> userManager,
        MembershipDbContext membershipDbContext,
        IIdentityEmailService identityEmailService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        ILogger<RegistrationService> logger)
    {
        _userManager = userManager;
        _membershipDbContext = membershipDbContext;
        _identityEmailService = identityEmailService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RegistrationResult> RegisterAsync(RegisterViewModel model)
    {
        try
        {
            // Check for duplicate email
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return RegistrationResult.Failure("An account with this email address already exists.");
            }

            // Create ApplicationUser with EmailConfirmed = false, BusinessId = null
            // No UserBusiness or UserBusinessPermission records are created
            var user = new ApplicationUser
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

            // Create PendingRegistration record with selected PlanId
            var pendingRegistration = new PendingRegistration
            {
                UserId = user.Id,
                PlanId = model.SelectedPlanId!.Value,
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
}
