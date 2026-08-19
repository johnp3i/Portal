/*
    Migration: 170_AddTaskOutcomeToFollowUpTask
    Description: Adds TaskOutcome NVARCHAR(20) NULL column to [sales].[FollowUpTask]
                 for closure classification: "Completed", "Unprocessed", or NULL (open).
                 Backfills existing completed tasks with TaskOutcome = 'Completed'.
    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

-- =============================================================================
-- 1. Add [TaskOutcome] column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales'
      AND TABLE_NAME = 'FollowUpTask'
      AND COLUMN_NAME = 'TaskOutcome'
)
BEGIN
    ALTER TABLE [sales].[FollowUpTask]
        ADD [TaskOutcome] NVARCHAR(20) NULL;
END
GO

-- Backfill existing completed tasks (separate batch so column is visible)
UPDATE [sales].[FollowUpTask]
    SET [TaskOutcome] = 'Completed'
    WHERE [IsCompleted] = 1
      AND [TaskOutcome] IS NULL;
GO
