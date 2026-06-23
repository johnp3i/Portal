USE [Portal];
GO

/*
    Migration: 095_AddAccessLevelToPlanFeature
    Description: Adds [AccessLevel] column to [dbo].[PlanFeature].
                 Stores the permission level for a module within a plan:
                 'full', 'readonly', or 'none'.
                 Defaults to 'full' so existing records retain full access.

    Requirements: 1.2  - AccessLevel column (NVARCHAR(20), NOT NULL, DEFAULT 'full')

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add [AccessLevel] column to [dbo].[PlanFeature]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'PlanFeature'
      AND COLUMN_NAME = 'AccessLevel'
)
BEGIN
    ALTER TABLE [dbo].[PlanFeature]
        ADD [AccessLevel] NVARCHAR(20) NOT NULL
            CONSTRAINT [DF_PlanFeature_AccessLevel] DEFAULT ('full');
END
GO
