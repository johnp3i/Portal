/*
    Migration: 006_MigrateExistingUsersToUserBusiness
    Description: Migrates existing users from the legacy single-business model
                 (ApplicationUser.BusinessId) to the new multi-business model
                 (UserBusiness + UserBusinessPermission).

                 - Creates a UserBusiness record for each active user with a non-null BusinessId
                 - Grants full access to all 7 modules for each migrated UserBusiness record
                 - Does NOT create records for users with NULL BusinessId (SuperAdmin accounts)

    This script is idempotent — safe to run multiple times.
*/

-- ============================================================
-- Step 1: Create UserBusiness records from existing AspNetUsers.BusinessId
-- ============================================================
INSERT INTO [membership].[UserBusiness] ([UserId], [BusinessId], [IsDefault], [IsActive], [CreatedAtUtc])
SELECT
    AspNetUsers.[Id],
    AspNetUsers.[BusinessId],
    1,  -- IsDefault
    1,  -- IsActive
    GETUTCDATE()
FROM [dbo].[AspNetUsers]
WHERE AspNetUsers.[BusinessId] IS NOT NULL
  AND AspNetUsers.[IsActive] = 1
  AND NOT EXISTS (
      SELECT 1
      FROM [membership].[UserBusiness]
      WHERE [membership].[UserBusiness].[UserId] = AspNetUsers.[Id]
        AND [membership].[UserBusiness].[BusinessId] = AspNetUsers.[BusinessId]
  );
GO

-- ============================================================
-- Step 2: Grant full access to all 7 modules for migrated users
-- ============================================================
INSERT INTO [membership].[UserBusinessPermission] ([UserBusinessId], [Module], [AccessLevel], [IsActive], [CreatedAtUtc])
SELECT
    [membership].[UserBusiness].[Id],
    Modules.[Module],
    'full',
    1,  -- IsActive
    GETUTCDATE()
FROM [membership].[UserBusiness]
CROSS JOIN (
    VALUES ('customer'), ('quotation'), ('invoice'), ('revenue'), ('purchase'), ('vat'), ('audit')
) AS Modules([Module])
WHERE [membership].[UserBusiness].[IsActive] = 1
  AND NOT EXISTS (
      SELECT 1
      FROM [membership].[UserBusinessPermission]
      WHERE [membership].[UserBusinessPermission].[UserBusinessId] = [membership].[UserBusiness].[Id]
        AND [membership].[UserBusinessPermission].[Module] = Modules.[Module]
  );
GO
