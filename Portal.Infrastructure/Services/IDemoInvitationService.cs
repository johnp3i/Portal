using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service for managing demo access invitations — creation, validation,
/// token generation, access tracking, and administration (revoke/resend).
/// </summary>
public interface IDemoInvitationService
{
    /// <summary>
    /// Creates a new demo invitation, persists it with permissions, and triggers the invitation email.
    /// </summary>
    Task<DemoInvitation> CreateAsync(CreateDemoInvitationRequest request, string createdByUserId);

    /// <summary>
    /// Validates the invitation token, checks expiry/revocation status, and tracks access metrics.
    /// </summary>
    Task<DemoInvitationValidationResult> ValidateAndTrackAccessAsync(string token);

    /// <summary>
    /// Revokes an active invitation by setting status to 'revoked' and recording the revocation timestamp.
    /// </summary>
    Task RevokeAsync(int invitationId);

    /// <summary>
    /// Resends the invitation email for an existing active invitation.
    /// </summary>
    Task ResendEmailAsync(int invitationId);

    /// <summary>
    /// Gets a paginated list of all demo invitations with business name resolution.
    /// </summary>
    Task<PagedResult<DemoInvitationListItem>> GetAllPagedAsync(int page, int pageSize);

    /// <summary>
    /// Gets all businesses flagged as demo accounts (IsDemoAccount = 1).
    /// </summary>
    Task<List<DemoBusinessItem>> GetDemoBusinessesAsync();

    /// <summary>
    /// Gets the module permission dictionary for a given invitation (module → access level).
    /// Used by DemoPermissionFilter for enforcement.
    /// </summary>
    Task<Dictionary<string, string>> GetPermissionsForInvitationAsync(int invitationId);

    /// <summary>
    /// Generates a cryptographically secure, URL-safe Base64URL token (32 bytes, no padding).
    /// </summary>
    string GenerateToken();

    /// <summary>
    /// Ensures a UserBusiness record exists for the demo user and the specified business.
    /// </summary>
    Task EnsureDemoUserBusinessAsync(string userId, int businessId);

    /// <summary>
    /// Updates the module permissions for an existing invitation (delete + reinsert).
    /// </summary>
    Task UpdatePermissionsAsync(int invitationId, List<ModulePermissionEntry> permissions);
}
