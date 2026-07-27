using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Implementation of IInvitationService for managing invitation tokens and registration flow.
/// </summary>
public class InvitationService : IInvitationService
{
    private readonly MembershipDbContext _membershipDbContext;
    private readonly IBusinessPlanRepository _businessPlanRepository;
    private readonly IPlanRepository _planRepository;

    public InvitationService(
        MembershipDbContext membershipDbContext,
        IBusinessPlanRepository businessPlanRepository,
        IPlanRepository planRepository)
    {
        _membershipDbContext = membershipDbContext;
        _businessPlanRepository = businessPlanRepository;
        _planRepository = planRepository;
    }

    public async Task<Invitation> CreateInvitationAsync(string email, int businessId, string createdByUserId, List<InvitationModulePermission>? modulePermissions = null)
    {
        // --- Duplicate Invitation Check ---
        var existingPendingInvitation = await _membershipDbContext.Invitations
            .AnyAsync(i => i.Email == email && i.BusinessId == businessId && !i.IsUsed && i.ExpiresAtUtc > DateTime.UtcNow);

        if (existingPendingInvitation)
        {
            throw new InvalidOperationException("An invitation has already been sent to this email address.");
        }

        // --- Already a Member Check ---
        var userWithEmail = await _membershipDbContext.Users
            .Where(u => u.NormalizedEmail == email.ToUpper())
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userWithEmail != null)
        {
            var alreadyMember = await _membershipDbContext.UserBusinesses
                .AnyAsync(ub => ub.UserId == userWithEmail && ub.BusinessId == businessId && ub.IsActive);

            if (alreadyMember)
            {
                throw new InvalidOperationException("This user already has access to your business.");
            }
        }

        // --- User Limit Enforcement ---
        var activePlan = await _businessPlanRepository.GetActiveByBusinessIdAsync(businessId);
        if (activePlan == null)
        {
            throw new InvalidOperationException("Cannot create invitation: no active subscription plan found for this business.");
        }

        var plan = await _planRepository.GetByIdAsync(activePlan.PlanId);
        var maxUsers = plan!.MaxUsers;
        if (maxUsers != -1)
        {
            var activeUserCount = await _membershipDbContext.UserBusinesses
                .CountAsync(ub => ub.BusinessId == businessId && ub.IsActive);

            var pendingInvitationCount = await _membershipDbContext.Invitations
                .CountAsync(i => i.BusinessId == businessId && !i.IsUsed && i.ExpiresAtUtc > DateTime.UtcNow);

            var occupiedSeats = activeUserCount + pendingInvitationCount;
            if (occupiedSeats >= maxUsers)
            {
                throw new InvalidOperationException(
                    $"Cannot create invitation: the user limit of {maxUsers} has been reached for this business. " +
                    $"Current seats occupied: {occupiedSeats} (active users: {activeUserCount}, pending invitations: {pendingInvitationCount}).");
            }
        }

        if (modulePermissions != null && modulePermissions.Count > 0)
        {
            foreach (var permission in modulePermissions)
            {
                if (!PortalModules.IsValid(permission.Module))
                {
                    throw new ArgumentException($"Invalid module name: '{permission.Module}'. Valid modules are: {string.Join(", ", PortalModules.All)}.");
                }

                if (!AccessLevels.IsValid(permission.AccessLevel))
                {
                    throw new ArgumentException($"Invalid access level: '{permission.AccessLevel}'. Valid access levels are: {string.Join(", ", AccessLevels.All)}.");
                }
            }
        }

        var invitation = new Invitation
        {
            Email = email,
            BusinessId = businessId,
            Token = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(72),
            IsUsed = false,
            CreatedByUserId = createdByUserId,
            ModulePermissionsJson = modulePermissions != null && modulePermissions.Count > 0
                ? JsonSerializer.Serialize(modulePermissions)
                : null
        };

        _membershipDbContext.Invitations.Add(invitation);
        await _membershipDbContext.SaveChangesAsync();

        return invitation;
    }

    public async Task<Invitation?> ValidateTokenAsync(string token)
    {
        var invitation = await _membershipDbContext.Invitations
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation == null) return null;
        if (invitation.IsUsed) return null;
        if (DateTime.UtcNow > invitation.ExpiresAtUtc) return null;

        return invitation;
    }

    public async Task MarkAsUsedAsync(int invitationId)
    {
        var invitation = await _membershipDbContext.Invitations.FindAsync(invitationId);
        if (invitation != null)
        {
            invitation.IsUsed = true;
            await _membershipDbContext.SaveChangesAsync();
        }
    }

    public async Task<List<Invitation>> GetAllInvitationsAsync()
    {
        return await _membershipDbContext.Invitations
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task CancelInvitationAsync(int invitationId)
    {
        var invitation = await _membershipDbContext.Invitations.FindAsync(invitationId);
        if (invitation != null && !invitation.IsUsed)
        {
            _membershipDbContext.Invitations.Remove(invitation);
            await _membershipDbContext.SaveChangesAsync();
        }
    }

    public async Task CreateUserBusinessFromInvitationAsync(string userId, Invitation invitation)
    {
        try
        {
            // 1. Create UserBusiness record
            var userBusiness = new UserBusiness
            {
                UserId = userId,
                BusinessId = invitation.BusinessId,
                IsDefault = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            _membershipDbContext.UserBusinesses.Add(userBusiness);
            await _membershipDbContext.SaveChangesAsync();

            // 2. Deserialize ModulePermissionsJson
            List<InvitationModulePermission>? permissions = null;

            if (!string.IsNullOrWhiteSpace(invitation.ModulePermissionsJson))
            {
                try
                {
                    permissions = JsonSerializer.Deserialize<List<InvitationModulePermission>>(invitation.ModulePermissionsJson);
                }
                catch (JsonException)
                {
                    // Malformed JSON — fall through to default "none" for all modules
                    permissions = null;
                }
            }

            // 3. Create UserBusinessPermission records
            if (permissions != null && permissions.Count > 0)
            {
                // Create permissions from the invitation specification
                foreach (var permission in permissions)
                {
                    var userBusinessPermission = new UserBusinessPermission
                    {
                        UserBusinessId = userBusiness.Id,
                        Module = permission.Module,
                        AccessLevel = permission.AccessLevel,
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    _membershipDbContext.UserBusinessPermissions.Add(userBusinessPermission);
                }
            }
            else
            {
                // 4. If null, empty, or malformed: assign "none" for all 7 modules
                foreach (var module in PortalModules.All)
                {
                    var userBusinessPermission = new UserBusinessPermission
                    {
                        UserBusinessId = userBusiness.Id,
                        Module = module,
                        AccessLevel = AccessLevels.None,
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    _membershipDbContext.UserBusinessPermissions.Add(userBusinessPermission);
                }
            }

            // 5. Save changes
            await _membershipDbContext.SaveChangesAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
}
