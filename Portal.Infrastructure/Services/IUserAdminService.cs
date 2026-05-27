using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service interface for user administration operations including listing users,
/// toggling active status, and managing per-module permissions.
/// </summary>
public interface IUserAdminService
{
    /// <summary>
    /// Returns a paginated list of business users scoped to the current tenant.
    /// </summary>
    Task<PagedResult<UserAdminDto>> GetUsersAsync(UserAdminFilter filter);

    /// <summary>
    /// Deactivates a user by setting IsActive=false and recording an audit entry.
    /// </summary>
    Task<ServiceResult> DeactivateUserAsync(int userBusinessId, string performedByUserId);

    /// <summary>
    /// Reactivates a user by setting IsActive=true and recording an audit entry.
    /// </summary>
    Task<ServiceResult> ReactivateUserAsync(int userBusinessId, string performedByUserId);

    /// <summary>
    /// Returns one UserModulePermissionDto per module in PortalModules.All,
    /// defaulting to AccessLevel="none", IsActive=false, PermissionId=null for modules
    /// with no existing record.
    /// </summary>
    Task<List<UserModulePermissionDto>> GetUserPermissionsAsync(int userBusinessId);

    /// <summary>
    /// Validates module and access level, upserts the permission record, and writes an audit entry.
    /// Audit log failures are logged and swallowed — they do not fail the primary operation.
    /// </summary>
    Task<ServiceResult> UpdatePermissionAsync(
        int userBusinessId, string module, string accessLevel, string performedByUserId);
}
