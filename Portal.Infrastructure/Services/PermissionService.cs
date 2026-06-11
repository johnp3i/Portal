using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;

namespace Portal.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly MembershipDbContext _dbContext;
    private readonly PortalDbContext _portalDbContext;
    private readonly ICurrentTenantService _tenantService;

    public PermissionService(MembershipDbContext dbContext, PortalDbContext portalDbContext, ICurrentTenantService tenantService)
    {
        _dbContext = dbContext;
        _portalDbContext = portalDbContext;
        _tenantService = tenantService;
    }

    public async Task<string> GetAccessLevelAsync(string userId, string module, int? businessId = null)
    {
        try
        {
            var resolvedBusinessId = businessId ?? _tenantService.CurrentBusinessId;

            var permission = await _dbContext.UserBusinessPermissions
                .Where(p => p.UserBusiness.UserId == userId
                         && p.UserBusiness.BusinessId == resolvedBusinessId
                         && p.UserBusiness.IsActive
                         && p.Module == module
                         && p.IsActive)
                .Select(p => p.AccessLevel)
                .FirstOrDefaultAsync();

            return permission ?? AccessLevels.None;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Dictionary<string, string>> GetAllAccessLevelsAsync(string userId, int? businessId = null)
    {
        try
        {
            var resolvedBusinessId = businessId ?? _tenantService.CurrentBusinessId;

            var permissions = await _dbContext.UserBusinessPermissions
                .Where(p => p.UserBusiness.UserId == userId
                         && p.UserBusiness.BusinessId == resolvedBusinessId
                         && p.UserBusiness.IsActive
                         && p.IsActive)
                .ToDictionaryAsync(p => p.Module, p => p.AccessLevel);

            return permissions;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Dictionary<string, string>> GetDemoPermissionsAsync(int invitationId)
    {
        try
        {
            var permissions = await _portalDbContext.DemoInvitationPermissions
                .Where(p => p.DemoInvitationId == invitationId)
                .ToDictionaryAsync(p => p.Module, p => p.AccessLevel);

            return permissions;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
