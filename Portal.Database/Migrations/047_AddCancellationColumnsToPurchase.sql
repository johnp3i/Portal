/*
    Migration: 047_AddCancellationColumnsToPurchase
    Description: Adds IsCancelled BIT column and CancelledAtUtc DATETIME2 column to [purchase].[Purchase]
                 for purchase cancellation support.
                 IsCancelled has a named default constraint. CancelledAtUtc is nullable (only populated on cancel).
    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add [IsCancelled] column with named default constraint
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'Purchase'
      AND COLUMN_NAME = 'IsCancelled'
)
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD [IsCancelled] BIT NOT NULL
        CONSTRAINT [DF_Purchase_IsCancelled] DEFAULT (0);
END
GO

-- =============================================================================
-- 2. Add [CancelledAtUtc] column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'Purchase'
      AND COLUMN_NAME = 'CancelledAtUtc'
)
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD [CancelledAtUtc] DATETIME2 NULL;
END
GO
