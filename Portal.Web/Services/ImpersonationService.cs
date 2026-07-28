using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Serilog;

namespace Portal.Web.Services;

/// <summary>
/// Handles SuperAdmin user impersonation — signing in as another user
/// while preserving the ability to return to the original admin session.
/// </summary>
public class ImpersonationService
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MembershipDbContext _membershipDbContext;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _tenantService;

    public const string IsImpersonatingClaim = "IsImpersonating";
    public const string OriginalUserIdClaim = "OriginalUserId";
    public const string OriginalUserNameClaim = "OriginalUserName";

    public ImpersonationService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        MembershipDbContext membershipDbContext,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService tenantService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _membershipDbContext = membershipDbContext;
        _auditLogRepository = auditLogRepository;
        _tenantService = tenantService;
    }

    /// <summary>
    /// Signs in as the target user, storing the original admin identity in claims.
    /// </summary>
    public async Task<ServiceResult> StartImpersonationAsync(int targetUserBusinessId, ClaimsPrincipal currentUser)
    {
        try
        {
            // 1. Validate caller is SuperAdmin
            if (!currentUser.IsInRole("SuperAdmin"))
                return ServiceResult.Fail("Only SuperAdmin can impersonate users.");

            // 2. Prevent impersonating while already impersonating
            if (currentUser.HasClaim(IsImpersonatingClaim, "true"))
                return ServiceResult.Fail("You are already impersonating a user. Return to your account first.");

            // 3. Load target UserBusiness
            var targetUserBusiness = await _membershipDbContext.UserBusinesses
                .Include(ub => ub.User)
                .FirstOrDefaultAsync(ub => ub.Id == targetUserBusinessId);

            if (targetUserBusiness == null)
                return ServiceResult.Fail("Target user not found.");

            var targetUser = targetUserBusiness.User;

            // 4. Cannot impersonate another SuperAdmin
            var isTargetSuperAdmin = await _userManager.IsInRoleAsync(targetUser, "SuperAdmin");
            if (isTargetSuperAdmin)
                return ServiceResult.Fail("Cannot impersonate another SuperAdmin.");

            // 5. Store original identity
            var originalUserId = currentUser.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var originalUserName = currentUser.Identity?.Name ?? "SuperAdmin";

            // 6. Build impersonation claims
            var additionalClaims = new List<Claim>
            {
                new Claim(IsImpersonatingClaim, "true"),
                new Claim(OriginalUserIdClaim, originalUserId),
                new Claim(OriginalUserNameClaim, originalUserName)
            };

            // 7. Sign in as target user with additional impersonation claims
            await _signInManager.SignInWithClaimsAsync(
                targetUser,
                new Microsoft.AspNetCore.Authentication.AuthenticationProperties { IsPersistent = false },
                additionalClaims);

            // 8. Audit log
            try
            {
                await _auditLogRepository.InsertAsync(new AuditLog
                {
                    BusinessId = _tenantService.CurrentBusinessId,
                    UserId = originalUserId,
                    Action = "ImpersonationStarted",
                    TableName = "UserBusiness",
                    RecordId = targetUserBusinessId.ToString(),
                    NewValues = $"SuperAdmin ({originalUserName}) started impersonating {targetUser.FirstName} {targetUser.LastName} ({targetUser.Email})",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to write impersonation audit log");
            }

            return new ServiceResult { Success = true, Message = $"Now viewing as {targetUser.FirstName} {targetUser.LastName}." };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "StartImpersonationAsync failed for targetUserBusinessId={TargetId}", targetUserBusinessId);
            throw;
        }
    }

    /// <summary>
    /// Ends impersonation and restores the original SuperAdmin session.
    /// </summary>
    public async Task<ServiceResult> EndImpersonationAsync(ClaimsPrincipal currentUser)
    {
        try
        {
            // 1. Validate currently impersonating
            if (!currentUser.HasClaim(IsImpersonatingClaim, "true"))
                return ServiceResult.Fail("You are not currently impersonating anyone.");

            // 2. Get original admin identity
            var originalUserId = currentUser.FindFirstValue(OriginalUserIdClaim);
            if (string.IsNullOrEmpty(originalUserId))
                return ServiceResult.Fail("Cannot determine original admin identity. Please log out and back in.");

            // 3. Load original admin user
            var originalUser = await _userManager.FindByIdAsync(originalUserId);
            if (originalUser == null)
                return ServiceResult.Fail("Original admin account not found.");

            // 4. Get impersonated user info for audit
            var impersonatedUserId = currentUser.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
            var originalUserName = currentUser.FindFirstValue(OriginalUserNameClaim) ?? "SuperAdmin";

            // 5. Sign back in as original admin (no additional impersonation claims)
            await _signInManager.SignInAsync(originalUser, new Microsoft.AspNetCore.Authentication.AuthenticationProperties { IsPersistent = false });

            // 6. Audit log
            try
            {
                await _auditLogRepository.InsertAsync(new AuditLog
                {
                    BusinessId = _tenantService.CurrentBusinessId,
                    UserId = originalUserId,
                    Action = "ImpersonationEnded",
                    TableName = "UserBusiness",
                    RecordId = impersonatedUserId,
                    NewValues = $"SuperAdmin ({originalUserName}) ended impersonation",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to write impersonation end audit log");
            }

            return new ServiceResult { Success = true, Message = "Returned to admin account." };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EndImpersonationAsync failed");
            throw;
        }
    }
}
