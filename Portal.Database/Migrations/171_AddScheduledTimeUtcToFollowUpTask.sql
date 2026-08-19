-- ============================================================
-- Migration: 171_AddScheduledTimeUtcToFollowUpTask
-- Description: Adds optional ScheduledTimeUtc column to
--              [sales].[FollowUpTask] for time-of-day scheduling.
--              NULL indicates an all-day task; non-null stores
--              the time-of-day without fractional seconds.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales'
      AND TABLE_NAME = 'FollowUpTask'
      AND COLUMN_NAME = 'ScheduledTimeUtc'
)
BEGIN
    ALTER TABLE [sales].[FollowUpTask]
        ADD [ScheduledTimeUtc] TIME(0) NULL;
END
GO
