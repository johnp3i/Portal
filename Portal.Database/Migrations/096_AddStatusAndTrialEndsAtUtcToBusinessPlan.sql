USE [Portal];
GO

/*
    Migration: 096_AddStatusAndTrialEndsAtUtcToBusinessPlan
    Description: Adds [Status] and [TrialEndsAtUtc] columns to [dbo].[BusinessPlan].
                 Status tracks the subscription lifecycle state (active, trial, cancelled, expired).
                 TrialEndsAtUtc records when a trial period ends (nullable — not all businesses are on trial).

    Requirements: 1.3 - Status column (NVARCHAR(20), NOT NULL, DEFAULT 'active')
                 1.3 - TrialEndsAtUtc column (DATETIME2, NULL)

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add [Status] column to [dbo].[BusinessPlan]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'BusinessPlan'
      AND COLUMN_NAME = 'Status'
)
BEGIN
    ALTER TABLE [dbo].[BusinessPlan]
        ADD [Status] NVARCHAR(20) NOT NULL
            CONSTRAINT [DF_BusinessPlan_Status] DEFAULT ('active');
END
GO

-- =============================================================================
-- 2. Add [TrialEndsAtUtc] column to [dbo].[BusinessPlan]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'BusinessPlan'
      AND COLUMN_NAME = 'TrialEndsAtUtc'
)
BEGIN
    ALTER TABLE [dbo].[BusinessPlan]
        ADD [TrialEndsAtUtc] DATETIME2 NULL;
END
GO
