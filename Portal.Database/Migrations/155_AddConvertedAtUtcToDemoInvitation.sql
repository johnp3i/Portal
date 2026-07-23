-- ============================================================
-- Migration 155: Add ConvertedAtUtc to DemoInvitation
-- ============================================================
-- Purpose: Tracks when a demo invitation recipient converted to a
--          real paying account. NULL = not converted.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'portal' AND TABLE_NAME = 'DemoInvitation' AND COLUMN_NAME = 'ConvertedAtUtc'
)
BEGIN
    ALTER TABLE [portal].[DemoInvitation]
    ADD [ConvertedAtUtc] DATETIME2 NULL;

    PRINT 'Added [ConvertedAtUtc] column to [portal].[DemoInvitation].';
END
ELSE
BEGIN
    PRINT '[portal].[DemoInvitation].[ConvertedAtUtc] already exists.';
END
GO
