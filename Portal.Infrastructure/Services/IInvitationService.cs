using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service for managing invitation tokens and registration flow.
/// </summary>
public interface IInvitationService
{
    Task<Invitation> CreateInvitationAsync(string email, int businessId, string createdByUserId, List<InvitationModulePermission>? modulePermissions = null);
    Task<Invitation?> ValidateTokenAsync(string token);
    Task MarkAsUsedAsync(int invitationId);
    Task<List<Invitation>> GetAllInvitationsAsync();
    Task CancelInvitationAsync(int invitationId);

    /// <summary>
    /// Creates a UserBusiness record and associated UserBusinessPermission records
    /// based on the invitation's ModulePermissionsJson after user registration.
    /// </summary>
    Task CreateUserBusinessFromInvitationAsync(string userId, Invitation invitation);
}
