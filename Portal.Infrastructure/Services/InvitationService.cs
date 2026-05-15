using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Implementation of IInvitationService for managing invitation tokens and registration flow.
/// </summary>
public class InvitationService : IInvitationService
{
    private readonly MembershipDbContext _membershipDbContext;

    public InvitationService(MembershipDbContext membershipDbContext)
    {
        _membershipDbContext = membershipDbContext;
    }

    public async Task<Invitation> CreateInvitationAsync(string email, int businessId, string createdByUserId, List<InvitationModulePermission>? modulePermissions = null)
    {
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
