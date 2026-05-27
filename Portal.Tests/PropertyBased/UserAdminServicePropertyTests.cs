using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: audit-system-administration, Property 12: UpdatePermissionAsync stores correct access level and IsActive/DeactivatedAtUtc
// Feature: audit-system-administration, Property 13: DeactivateUserAsync/ReactivateUserAsync are inverses

/// <summary>
/// Property-based tests for UserAdminService.
/// Validates permission upsert correctness and activate/deactivate inverse operations.
/// Uses a mock UserAdminRepository to control state and verify outcomes without raw SQL.
/// **Validates: Requirements 5.3, 5.4, 5.5, 5.6, 6.5, 6.6**
/// </summary>
public class UserAdminServicePropertyTests
{
    private const int TestBusinessId = 10;
    private const string PerformedByUserId = "admin-user-id";

    #region Test Infrastructure

    /// <summary>
    /// Creates a UserAdminService wired to a mock UserAdminRepository and a no-op
    /// AuditLogRepository. The mock repository tracks permission state in a dictionary
    /// so tests can verify the stored values after service calls.
    /// </summary>
    private static (UserAdminService Service, Dictionary<string, UserBusinessPermission> PermissionStore,
        Dictionary<int, UserBusiness> UserStore)
        CreateServiceWithMocks()
    {
        var permissionStore = new Dictionary<string, UserBusinessPermission>();
        var userStore = new Dictionary<int, UserBusiness>();
        var nextPermId = 1;

        var repoMock = new Mock<UserAdminRepository>(MockBehavior.Loose,
            new DbContextOptionsBuilder<MembershipDbContext>()
                .UseInMemoryDatabase($"UserAdmin_{Guid.NewGuid()}")
                .Options);

        // GetByIdAsync — returns from userStore
        repoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => userStore.TryGetValue(id, out var ub) ? ub : null);

        // DeactivateAsync — updates userStore
        repoMock.Setup(r => r.DeactivateAsync(It.IsAny<int>(), It.IsAny<DateTime>()))
            .Returns((int id, DateTime dt) =>
            {
                if (userStore.TryGetValue(id, out var ub))
                {
                    ub.IsActive = false;
                    ub.DeactivatedAtUtc = dt;
                }
                return Task.CompletedTask;
            });

        // ReactivateAsync — updates userStore
        repoMock.Setup(r => r.ReactivateAsync(It.IsAny<int>()))
            .Returns((int id) =>
            {
                if (userStore.TryGetValue(id, out var ub))
                {
                    ub.IsActive = true;
                    ub.DeactivatedAtUtc = null;
                }
                return Task.CompletedTask;
            });

        // GetPermissionsAsync — returns from permissionStore
        repoMock.Setup(r => r.GetPermissionsAsync(It.IsAny<int>()))
            .ReturnsAsync((int userBusinessId) =>
                permissionStore.Values
                    .Where(p => p.UserBusinessId == userBusinessId)
                    .ToList());

        // UpsertPermissionAsync — inserts or updates in permissionStore
        repoMock.Setup(r => r.UpsertPermissionAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<DateTime?>()))
            .Returns((int userBusinessId, string module, string accessLevel,
                      bool isActive, DateTime? deactivatedAtUtc) =>
            {
                var key = $"{userBusinessId}:{module}";
                if (permissionStore.TryGetValue(key, out var existing))
                {
                    existing.AccessLevel = accessLevel;
                    existing.IsActive = isActive;
                    existing.DeactivatedAtUtc = deactivatedAtUtc;
                }
                else
                {
                    permissionStore[key] = new UserBusinessPermission
                    {
                        Id = nextPermId++,
                        UserBusinessId = userBusinessId,
                        Module = module,
                        AccessLevel = accessLevel,
                        IsActive = isActive,
                        DeactivatedAtUtc = deactivatedAtUtc,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                }
                return Task.CompletedTask;
            });

        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        // No-op audit log repository (failures are swallowed by the service anyway)
        var auditRepoMock = new Mock<AuditLogRepository>(MockBehavior.Loose,
            new DbContextOptionsBuilder<PortalDbContext>()
                .UseInMemoryDatabase($"AuditLog_{Guid.NewGuid()}")
                .Options,
            tenantMock.Object);
        auditRepoMock.Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        var service = new UserAdminService(
            repoMock.Object,
            auditRepoMock.Object,
            tenantMock.Object);

        return (service, permissionStore, userStore);
    }

    /// <summary>Creates a minimal UserBusiness record for testing.</summary>
    private static UserBusiness MakeUserBusiness(int id, bool isActive = true) => new()
    {
        Id = id,
        UserId = $"user-{id}",
        BusinessId = TestBusinessId,
        IsActive = isActive,
        DeactivatedAtUtc = isActive ? null : DateTime.UtcNow.AddDays(-1),
        CreatedAtUtc = DateTime.UtcNow,
        User = new ApplicationUser
        {
            Id = $"user-{id}",
            UserName = $"user{id}@test.com",
            Email = $"user{id}@test.com",
            FirstName = "Test",
            LastName = $"User{id}"
        }
    };

    #endregion

    #region Property 12: UpdatePermissionAsync stores correct access level and flags

    /// <summary>
    /// Property 12: For any user-module combination and any valid access level, after
    /// UpdatePermissionAsync the stored record reflects the new level:
    /// - "none" → IsActive=false, DeactivatedAtUtc non-null
    /// - "full"/"readonly" → IsActive=true, DeactivatedAtUtc=null
    /// **Validates: Requirements 5.3, 5.5, 5.6**
    /// </summary>
    // Feature: audit-system-administration, Property 12: for any user-module combination and any valid access level, after UpdatePermissionAsync the stored record reflects the new level
    [Property(MaxTest = 100)]
    public Property Property12_UpdatePermission_StoresCorrectAccessLevelAndFlags(
        PositiveInt userBusinessIdSeed,
        PositiveInt moduleSeed,
        PositiveInt accessLevelSeed)
    {
        var userBusinessId = (userBusinessIdSeed.Get % 50) + 1;
        var module = PortalModules.All[moduleSeed.Get % PortalModules.All.Length];
        var accessLevel = AccessLevels.All[accessLevelSeed.Get % AccessLevels.All.Length];

        var (service, permissionStore, _) = CreateServiceWithMocks();

        var result = service.UpdatePermissionAsync(
            userBusinessId, module, accessLevel, PerformedByUserId)
            .GetAwaiter().GetResult();

        if (!result.Success)
            return false.ToProperty().Label($"UpdatePermissionAsync failed: {result.Message}");

        var key = $"{userBusinessId}:{module}";
        if (!permissionStore.TryGetValue(key, out var stored))
            return false.ToProperty().Label("Permission record not found in store after upsert");

        // Verify access level stored correctly
        var accessLevelCorrect = stored.AccessLevel == accessLevel;

        // Verify IsActive and DeactivatedAtUtc based on access level
        bool isActiveCorrect;
        bool deactivatedAtCorrect;

        if (accessLevel == AccessLevels.None)
        {
            isActiveCorrect = !stored.IsActive;
            deactivatedAtCorrect = stored.DeactivatedAtUtc.HasValue;
        }
        else // "full" or "readonly"
        {
            isActiveCorrect = stored.IsActive;
            deactivatedAtCorrect = !stored.DeactivatedAtUtc.HasValue;
        }

        var allCorrect = accessLevelCorrect && isActiveCorrect && deactivatedAtCorrect;

        return allCorrect.ToProperty()
            .Label($"Module={module}, AccessLevel={accessLevel}, " +
                   $"StoredAccessLevel={stored.AccessLevel}, StoredIsActive={stored.IsActive}, " +
                   $"StoredDeactivatedAt={stored.DeactivatedAtUtc}, " +
                   $"AccessLevelCorrect={accessLevelCorrect}, IsActiveCorrect={isActiveCorrect}, " +
                   $"DeactivatedAtCorrect={deactivatedAtCorrect}");
    }

    /// <summary>
    /// Property 12b: UpdatePermissionAsync with an invalid module returns ServiceResult.Fail.
    /// UpdatePermissionAsync with an invalid access level returns ServiceResult.Fail.
    /// **Validates: Requirements 5.3**
    /// </summary>
    // Feature: audit-system-administration, Property 12: invalid module/access level returns ServiceResult.Fail
    [Property(MaxTest = 100)]
    public Property Property12b_UpdatePermission_InvalidInputs_ReturnFail(
        NonEmptyString invalidModule,
        NonEmptyString invalidAccessLevel)
    {
        // Ensure the generated strings are actually invalid
        var module = "invalid_module_" + invalidModule.Get;
        var accessLevel = "invalid_level_" + invalidAccessLevel.Get;

        var (service, _, _) = CreateServiceWithMocks();

        // Invalid module
        var resultInvalidModule = service.UpdatePermissionAsync(
            1, module, AccessLevels.Full, PerformedByUserId)
            .GetAwaiter().GetResult();

        // Invalid access level
        var resultInvalidLevel = service.UpdatePermissionAsync(
            1, PortalModules.Invoice, accessLevel, PerformedByUserId)
            .GetAwaiter().GetResult();

        var invalidModuleFails = !resultInvalidModule.Success;
        var invalidLevelFails = !resultInvalidLevel.Success;

        return (invalidModuleFails && invalidLevelFails).ToProperty()
            .Label($"InvalidModuleFails={invalidModuleFails}, InvalidLevelFails={invalidLevelFails}");
    }

    #endregion

    #region Property 13: DeactivateUserAsync and ReactivateUserAsync are inverses

    /// <summary>
    /// Property 13: DeactivateUserAsync → IsActive=false, DeactivatedAtUtc non-null.
    /// ReactivateUserAsync → IsActive=true, DeactivatedAtUtc=null.
    /// Operations are inverses: deactivate then reactivate yields IsActive=true, DeactivatedAtUtc=null.
    /// **Validates: Requirements 5.4, 6.5, 6.6**
    /// </summary>
    // Feature: audit-system-administration, Property 13: DeactivateUserAsync → IsActive=false, DeactivatedAtUtc non-null; ReactivateUserAsync → IsActive=true, DeactivatedAtUtc=null; operations are inverses
    [Property(MaxTest = 100)]
    public Property Property13_DeactivateAndReactivate_AreInverses(PositiveInt userBusinessIdSeed)
    {
        var userBusinessId = (userBusinessIdSeed.Get % 50) + 1;
        var (service, _, userStore) = CreateServiceWithMocks();

        // Seed an active user
        userStore[userBusinessId] = MakeUserBusiness(userBusinessId, isActive: true);

        // Step 1: Deactivate
        var deactivateResult = service.DeactivateUserAsync(userBusinessId, PerformedByUserId)
            .GetAwaiter().GetResult();

        if (!deactivateResult.Success)
            return false.ToProperty().Label($"DeactivateUserAsync failed: {deactivateResult.Message}");

        var afterDeactivate = userStore[userBusinessId];
        var deactivatedCorrectly = !afterDeactivate.IsActive && afterDeactivate.DeactivatedAtUtc.HasValue;

        // Step 2: Reactivate
        var reactivateResult = service.ReactivateUserAsync(userBusinessId, PerformedByUserId)
            .GetAwaiter().GetResult();

        if (!reactivateResult.Success)
            return false.ToProperty().Label($"ReactivateUserAsync failed: {reactivateResult.Message}");

        var afterReactivate = userStore[userBusinessId];
        var reactivatedCorrectly = afterReactivate.IsActive && !afterReactivate.DeactivatedAtUtc.HasValue;

        // Inverse property: deactivate then reactivate = original active state
        var inverseHolds = reactivatedCorrectly;

        var allCorrect = deactivatedCorrectly && reactivatedCorrectly && inverseHolds;

        return allCorrect.ToProperty()
            .Label($"UserBusinessId={userBusinessId}, " +
                   $"DeactivatedCorrectly={deactivatedCorrectly} " +
                   $"(IsActive={afterDeactivate.IsActive}, DeactivatedAt={afterDeactivate.DeactivatedAtUtc}), " +
                   $"ReactivatedCorrectly={reactivatedCorrectly} " +
                   $"(IsActive={afterReactivate.IsActive}, DeactivatedAt={afterReactivate.DeactivatedAtUtc}), " +
                   $"InverseHolds={inverseHolds}");
    }

    /// <summary>
    /// Property 13b: DeactivateUserAsync on a non-existent user returns ServiceResult.Fail.
    /// ReactivateUserAsync on a non-existent user returns ServiceResult.Fail.
    /// **Validates: Requirements 5.4**
    /// </summary>
    // Feature: audit-system-administration, Property 13: non-existent user returns ServiceResult.Fail
    [Property(MaxTest = 100)]
    public Property Property13b_DeactivateReactivate_NonExistentUser_ReturnFail(
        PositiveInt userBusinessIdSeed)
    {
        var userBusinessId = (userBusinessIdSeed.Get % 50) + 100; // IDs 100–149, not in store
        var (service, _, _) = CreateServiceWithMocks(); // Empty userStore

        var deactivateResult = service.DeactivateUserAsync(userBusinessId, PerformedByUserId)
            .GetAwaiter().GetResult();

        var reactivateResult = service.ReactivateUserAsync(userBusinessId, PerformedByUserId)
            .GetAwaiter().GetResult();

        var deactivateFails = !deactivateResult.Success;
        var reactivateFails = !reactivateResult.Success;

        return (deactivateFails && reactivateFails).ToProperty()
            .Label($"UserBusinessId={userBusinessId}, " +
                   $"DeactivateFails={deactivateFails}, ReactivateFails={reactivateFails}");
    }

    #endregion
}
