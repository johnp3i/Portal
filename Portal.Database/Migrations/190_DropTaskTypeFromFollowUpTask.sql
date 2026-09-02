-- ============================================================
-- Migration 190: Drop legacy [TaskType] column from [sales].[FollowUpTask]
-- ============================================================
-- Purpose: Phase 2 of the follow-up task type lookup conversion. After
--          Phase 1 (migrations 188/189) added [FollowUpTaskTypeId] and the
--          application was verified reading/writing the id, the free-text
--          [TaskType] NVARCHAR column is now redundant and is removed.
--          The display name is resolved from [sales].[FollowUpTaskTypes].
-- Schema: [sales]
-- Idempotent. Depends on migrations 188 and 189.
-- ============================================================

USE [Portal]
GO

-- Drop any default constraint bound to [TaskType] first (a column with a
-- bound default cannot be dropped directly). Discover the constraint name
-- dynamically since it may be auto-named.
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'FollowUpTask' AND COLUMN_NAME = 'TaskType'
)
BEGIN
    DECLARE @DefaultConstraintName SYSNAME;
    SELECT @DefaultConstraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('[sales].[FollowUpTask]')
      AND c.name = 'TaskType';

    IF @DefaultConstraintName IS NOT NULL
    BEGIN
        DECLARE @DropDefaultSql NVARCHAR(300) =
            N'ALTER TABLE [sales].[FollowUpTask] DROP CONSTRAINT ' + QUOTENAME(@DefaultConstraintName) + N';';
        EXEC sp_executesql @DropDefaultSql;
        PRINT 'Dropped default constraint ' + @DefaultConstraintName + ' on [TaskType].';
    END

    ALTER TABLE [sales].[FollowUpTask] DROP COLUMN [TaskType];
    PRINT 'Dropped [TaskType] column from [sales].[FollowUpTask].';
END
ELSE
BEGIN
    PRINT '[sales].[FollowUpTask].[TaskType] does not exist (already dropped).';
END
GO
