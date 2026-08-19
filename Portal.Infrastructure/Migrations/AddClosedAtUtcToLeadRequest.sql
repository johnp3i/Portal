-- ============================================================
-- Add ClosedAtUtc column to LeadRequest
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadRequest' AND COLUMN_NAME = 'ClosedAtUtc'
)
BEGIN
    ALTER TABLE [sales].[LeadRequest]
        ADD [ClosedAtUtc] DATETIME NULL;

    PRINT 'Added [ClosedAtUtc] column to [sales].[LeadRequest].';
END
ELSE
BEGIN
    PRINT '[sales].[LeadRequest].[ClosedAtUtc] already exists.';
END
GO
