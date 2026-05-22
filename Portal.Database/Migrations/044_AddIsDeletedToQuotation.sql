/*
    Migration: 044_AddIsDeletedToQuotation
    Description: Adds IsDeleted BIT column and DeletedAtUtc DATETIME2 column to [quotation].[Quotation] for soft-delete support.
                 IsDeleted has a named default constraint. DeletedAtUtc is nullable with no default (only populated on delete).
                 Existing rows default IsDeleted to 0 and DeletedAtUtc to NULL.

    Requirements: 2.1, 2.2, 2.3

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add [IsDeleted] column with named default constraint
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'Quotation'
      AND COLUMN_NAME = 'IsDeleted'
)
BEGIN
    ALTER TABLE [quotation].[Quotation]
        ADD [IsDeleted] BIT NOT NULL
        CONSTRAINT [DF_Quotation_IsDeleted] DEFAULT (0);
END
GO

-- =============================================================================
-- 2. Add [DeletedAtUtc] column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'Quotation'
      AND COLUMN_NAME = 'DeletedAtUtc'
)
BEGIN
    ALTER TABLE [quotation].[Quotation]
        ADD [DeletedAtUtc] DATETIME2 NULL;
END
GO
