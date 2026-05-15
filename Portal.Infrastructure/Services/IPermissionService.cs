namespace Portal.Infrastructure.Services;

public interface IPermissionService
{
    /// <summary>
    /// Gets the access level for a specific module. Returns "none" if no active record exists.
    /// Uses ICurrentTenantService.CurrentBusinessId when businessId is null.
    /// </summary>
    Task<string> GetAccessLevelAsync(string userId, string module, int? businessId = null);

    /// <summary>
    /// Gets all module access levels for the current user/business combination.
    /// Returns a dictionary of module → accessLevel.
    /// </summary>
    Task<Dictionary<string, string>> GetAllAccessLevelsAsync(string userId, int? businessId = null);
}
