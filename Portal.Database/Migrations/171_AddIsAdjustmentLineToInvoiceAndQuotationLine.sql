/*
    Migration: 171_AddIsAdjustmentLineToInvoiceAndQuotationLine
    Description: Adds IsAdjustmentLine BIT NOT NULL DEFAULT (0) column to
                 [invoice].[InvoiceLine] and [quotation].[QuotationLine].
                 Used to flag system-managed bulk discount adjustment lines.
                 Existing rows remain unaffected (default to 0).
    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

-- =============================================================================
-- 1. Add [IsAdjustmentLine] to [invoice].[InvoiceLine]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceLine'
      AND COLUMN_NAME = 'IsAdjustmentLine'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [IsAdjustmentLine] BIT NOT NULL CONSTRAINT [DF_InvoiceLine_IsAdjustmentLine] DEFAULT (0);
END
GO

-- =============================================================================
-- 2. Add [IsAdjustmentLine] to [quotation].[QuotationLine]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'QuotationLine'
      AND COLUMN_NAME = 'IsAdjustmentLine'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [IsAdjustmentLine] BIT NOT NULL CONSTRAINT [DF_QuotationLine_IsAdjustmentLine] DEFAULT (0);
END
GO

-- =============================================================================
-- 3. Filtered unique index: at most one adjustment line per invoice
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_InvoiceLine_AdjustmentLine'
      AND object_id = OBJECT_ID(N'[invoice].[InvoiceLine]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_InvoiceLine_AdjustmentLine]
    ON [invoice].[InvoiceLine] ([InvoiceId])
    WHERE [IsAdjustmentLine] = 1;
END
GO

-- =============================================================================
-- 4. Filtered unique index: at most one adjustment line per quotation
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_QuotationLine_AdjustmentLine'
      AND object_id = OBJECT_ID(N'[quotation].[QuotationLine]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_QuotationLine_AdjustmentLine]
    ON [quotation].[QuotationLine] ([QuotationId])
    WHERE [IsAdjustmentLine] = 1;
END
GO
