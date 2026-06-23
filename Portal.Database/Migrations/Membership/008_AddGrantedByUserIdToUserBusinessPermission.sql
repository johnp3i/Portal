USE [Portal.Membership];
GO

/*
    Migration: 008_AddGrantedByUserIdToUserBusinessPermission
    Description: Adds [GrantedByUserId] column to [membership].[UserBusinessPermission].
                 Records which user granted the permission (nullable for existing records).

    Requirements: 1.4, 1.5 - GrantedByUserId (NVARCHAR(450), NULL)

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add [GrantedByUserId] column to [membership].[UserBusinessPermission]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'membership'
      AND TABLE_NAME = 'UserBusinessPermission'
      AND COLUMN_NAME = 'GrantedByUserId'
)
BEGIN
    ALTER TABLE [membership].[UserBusinessPermission]
        ADD [GrantedByUserId] NVARCHAR(450) NULL;
END
GO
