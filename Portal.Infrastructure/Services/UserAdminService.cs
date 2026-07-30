using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Serilog;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service for user administration: listing users, toggling active status,
/// and managing per-module permissions. Scoped to the current tenant via ICurrentTenantService.
/// Audit log write failures are caught, logged via Serilog, and swallowed — they never
/// fail the primary operation.
/// </summary>
public class UserAdminService : IUserAdminService
{
    private readonly UserAdminRepository _userAdminRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _currentTenantService;

    public UserAdminService(
        UserAdminRepository userAdminRepository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService currentTenantService)
    {
        _userAdminRepository = userAdminRepository;
        _auditLogRepository = auditLogRepository;
        _currentTenantService = currentTenantService;
    }

    /// <inheritdoc />
    public async Task<PagedResult<UserAdminDto>> GetUsersAsync(UserAdminFilter filter)
    {
        // Clamp pagination
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        // Resolve status filter
        bool? isActive = filter.StatusFilter switch
        {
            "Active" => true,
            "Inactive" => false,
            _ => null
        };

        var skip = (pageNumber - 1) * pageSize;

        var (items, totalCount) = await _userAdminRepository.GetUsersPagedAsync(
            null,
            filter.SearchTerm,
            isActive,
            skip,
            pageSize);

        var dtos = items.Select(ub => new UserAdminDto
        {
            UserBusinessId = ub.Id,
            UserId = ub.UserId,
            FullName = ub.User.FirstName + " " + ub.User.LastName,
            Email = ub.User.Email ?? string.Empty,
            Role = "User",
            IsActive = ub.IsActive,
            LastLoginUtc = ub.User.LastLoginUtc,
            BusinessId = ub.BusinessId
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedResult<UserAdminDto>
        {
            Items = dtos,
            CurrentPage = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeactivateUserAsync(int userBusinessId, string performedByUserId)
    {
        var userBusiness = await _userAdminRepository.GetByIdAsync(userBusinessId);
        if (userBusiness == null)
        {
            return ServiceResult.Fail("User not found.");
        }

        var deactivatedAt = DateTime.UtcNow;
        await _userAdminRepository.DeactivateAsync(userBusinessId, deactivatedAt);

        try
        {
            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = _currentTenantService.CurrentBusinessId,
                UserId = performedByUserId,
                Action = "Update",
                TableName = "UserBusiness",
                RecordId = userBusinessId.ToString(),
                OldValues = "{\"IsActive\":true}",
                NewValues = "{\"IsActive\":false}",
                Timestamp = deactivatedAt
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to write audit log for DeactivateUserAsync. UserBusinessId={UserBusinessId}", userBusinessId);
        }

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ReactivateUserAsync(int userBusinessId, string performedByUserId)
    {
        var userBusiness = await _userAdminRepository.GetByIdAsync(userBusinessId);
        if (userBusiness == null)
        {
            return ServiceResult.Fail("User not found.");
        }

        await _userAdminRepository.ReactivateAsync(userBusinessId);

        try
        {
            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = _currentTenantService.CurrentBusinessId,
                UserId = performedByUserId,
                Action = "Update",
                TableName = "UserBusiness",
                RecordId = userBusinessId.ToString(),
                OldValues = "{\"IsActive\":false}",
                NewValues = "{\"IsActive\":true}",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to write audit log for ReactivateUserAsync. UserBusinessId={UserBusinessId}", userBusinessId);
        }

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<List<UserModulePermissionDto>> GetUserPermissionsAsync(int userBusinessId)
    {
        var existingPermissions = await _userAdminRepository.GetPermissionsAsync(userBusinessId);

        var permissionMap = existingPermissions.ToDictionary(p => p.Module, p => p);

        var result = PortalModules.All.Select(module =>
        {
            if (permissionMap.TryGetValue(module, out var existing))
            {
                return new UserModulePermissionDto
                {
                    PermissionId = existing.Id,
                    Module = module,
                    AccessLevel = existing.AccessLevel,
                    IsActive = existing.IsActive
                };
            }

            return new UserModulePermissionDto
            {
                PermissionId = null,
                Module = module,
                AccessLevel = AccessLevels.None,
                IsActive = false
            };
        }).ToList();

        return result;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdatePermissionAsync(
        int userBusinessId, string module, string accessLevel, string performedByUserId)
    {
        if (!PortalModules.IsValid(module))
        {
            return ServiceResult.Fail($"Invalid module: '{module}'.");
        }

        if (!AccessLevels.IsValid(accessLevel))
        {
            return ServiceResult.Fail($"Invalid access level: '{accessLevel}'.");
        }

        // Determine IsActive and DeactivatedAtUtc based on access level
        bool isActive;
        DateTime? deactivatedAtUtc;

        if (accessLevel == AccessLevels.None)
        {
            isActive = false;
            deactivatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            isActive = true;
            deactivatedAtUtc = null;
        }

        await _userAdminRepository.UpsertPermissionAsync(userBusinessId, module, accessLevel, isActive, deactivatedAtUtc);

        // Resolve the permission record id for the audit entry
        var permissions = await _userAdminRepository.GetPermissionsAsync(userBusinessId);
        var upserted = permissions.FirstOrDefault(p => p.Module == module);
        var permissionId = upserted?.Id ?? 0;

        try
        {
            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = _currentTenantService.CurrentBusinessId,
                UserId = performedByUserId,
                Action = "Update",
                TableName = "UserBusinessPermission",
                RecordId = permissionId.ToString(),
                NewValues = $"{{\"Module\":\"{module}\",\"AccessLevel\":\"{accessLevel}\",\"IsActive\":{isActive.ToString().ToLower()}}}",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to write audit log for UpdatePermissionAsync. UserBusinessId={UserBusinessId}, Module={Module}", userBusinessId, module);
        }

        return ServiceResult.Ok();
    }
}
