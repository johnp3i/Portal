/*
    Migration: 043_AddIsDeletedToInvoice
    Description: Adds IsDeleted BIT column and DeletedAtUtc DATETIME2 column to [invoice].[Invoice] for soft-delete support.
                 IsDeleted has a named default constraint. DeletedAtUtc is nullable with no default (only populated on delete).
                 Includes a composite index for filtered queries.
    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add [IsDeleted] column with named default constraint
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'Invoice'
      AND COLUMN_NAME = 'IsDeleted'
)
BEGIN
    ALTER TABLE [invoice].[Invoice]
        ADD [IsDeleted] BIT NOT NULL
        CONSTRAINT [DF_Invoice_IsDeleted] DEFAULT (0);
END
GO

-- =============================================================================
-- 2. Add [DeletedAtUtc] column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'Invoice'
      AND COLUMN_NAME = 'DeletedAtUtc'
)
BEGIN
    ALTER TABLE [invoice].[Invoice]
        ADD [DeletedAtUtc] DATETIME2 NULL;
END
GO

-- =============================================================================
-- 3. Create composite non-clustered index on (BusinessId, IsDeleted)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Invoice_BusinessId_IsDeleted'
      AND [object_id] = OBJECT_ID('[invoice].[Invoice]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoice_BusinessId_IsDeleted]
        ON [invoice].[Invoice] ([BusinessId], [IsDeleted]);
END
GO
