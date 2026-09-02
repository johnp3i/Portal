-- ============================================================
-- Migration 189: Add + backfill [FollowUpTaskTypeId] on [sales].[FollowUpTask]
-- ============================================================
-- Purpose: Add the FK column that references [sales].[FollowUpTaskTypes],
--          backfill it from the existing free-text [TaskType], enforce the
--          FK and NOT NULL. The legacy [TaskType] column is RETAINED in this
--          phase (Phase 1) and kept in sync by application writes. It is
--          dropped later in Phase 2 after verification.
-- Schema: [sales]
-- Idempotent. Depends on migration 188 (lookup table + seed).
-- ============================================================

USE [Portal]
GO

-- 1. Add nullable column (idempotent)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'FollowUpTask' AND COLUMN_NAME = 'FollowUpTaskTypeId'
)
BEGIN
    ALTER TABLE [sales].[FollowUpTask] ADD [FollowUpTaskTypeId] TINYINT NULL;
    PRINT 'Added [FollowUpTaskTypeId] column to [sales].[FollowUpTask].';
END
ELSE
BEGIN
    PRINT '[sales].[FollowUpTask].[FollowUpTaskTypeId] already exists.';
END
GO

-- 2. Backfill by matching the existing TaskType text to the lookup Name.
--    Any value that does not match a lookup name falls back to 'Other',
--    guaranteeing no row is left NULL before the NOT NULL step.
--    Safe to re-run: only NULL FollowUpTaskTypeId rows are touched.
IF EXISTS (
    SELECT 1 FROM [sales].[FollowUpTask] WHERE [FollowUpTaskTypeId] IS NULL
)
BEGIN
    UPDATE [sales].[FollowUpTask]
       SET [FollowUpTaskTypeId] = COALESCE(
            (SELECT [Id] FROM [sales].[FollowUpTaskTypes]
              WHERE [Name] = [sales].[FollowUpTask].[TaskType]),
            (SELECT [Id] FROM [sales].[FollowUpTaskTypes] WHERE [Name] = 'Other'))
     WHERE [FollowUpTaskTypeId] IS NULL;

    PRINT 'Backfilled [FollowUpTaskTypeId] from [TaskType] (unmatched -> Other).';
END
GO

-- 3. Foreign key (idempotent)
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_FollowUpTask_FollowUpTaskType'
      AND parent_object_id = OBJECT_ID('[sales].[FollowUpTask]')
)
BEGIN
    ALTER TABLE [sales].[FollowUpTask]
        ADD CONSTRAINT [FK_FollowUpTask_FollowUpTaskType]
            FOREIGN KEY ([FollowUpTaskTypeId]) REFERENCES [sales].[FollowUpTaskTypes]([Id]);
    PRINT 'Added FK_FollowUpTask_FollowUpTaskType.';
END
GO

-- 4. Enforce NOT NULL after backfill
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'FollowUpTask'
      AND COLUMN_NAME = 'FollowUpTaskTypeId' AND IS_NULLABLE = 'YES'
)
BEGIN
    ALTER TABLE [sales].[FollowUpTask] ALTER COLUMN [FollowUpTaskTypeId] TINYINT NOT NULL;
    PRINT 'Set [FollowUpTaskTypeId] NOT NULL.';
END
GO
