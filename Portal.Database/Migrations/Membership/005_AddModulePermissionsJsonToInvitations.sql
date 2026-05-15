/*
    Migration: 005_AddModulePermissionsJsonToInvitations
    Description: Adds the [ModulePermissionsJson] column to the [dbo].[Invitations] table.
                 This column stores a JSON-serialized list of module permissions to apply
                 when the invited user completes registration.

                 Format: [{"Module":"customer","AccessLevel":"full"}, ...]

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Invitations]')
      AND name = N'ModulePermissionsJson'
)
BEGIN
    ALTER TABLE [dbo].[Invitations]
        ADD [ModulePermissionsJson] NVARCHAR(MAX) NULL;
END
GO
