-- ============================================================
-- Adds DeletedAtUtc column to [document].[DocumentAttachment]
-- to record the timestamp of soft-deletion.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[document].[DocumentAttachment]')
      AND name = 'DeletedAtUtc'
)
BEGIN
    ALTER TABLE [document].[DocumentAttachment]
        ADD [DeletedAtUtc] DATETIME NULL;
END
GO
