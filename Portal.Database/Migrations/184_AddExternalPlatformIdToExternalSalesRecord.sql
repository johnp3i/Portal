-- ============================================================
-- Migration 184: Add ExternalPlatformId to ExternalSalesRecord
-- ============================================================
-- Purpose: Tags an imported sales record to the external platform it
--          came from. A record is sourced by EITHER RevenueSourceId
--          (POS device) OR ExternalPlatformId (external system);
--          both are nullable.
-- Schema: [revenue]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'revenue' AND TABLE_NAME = 'ExternalSalesRecord' AND COLUMN_NAME = 'ExternalPlatformId'
)
BEGIN
    ALTER TABLE [revenue].[ExternalSalesRecord]
        ADD [ExternalPlatformId] INT NULL;

    PRINT 'Added [ExternalPlatformId] to [revenue].[ExternalSalesRecord].';
END
ELSE
BEGIN
    PRINT '[ExternalPlatformId] already exists on [revenue].[ExternalSalesRecord].';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = 'FK_ExternalSalesRecord_ExternalPlatform'
      AND [parent_object_id] = OBJECT_ID('[revenue].[ExternalSalesRecord]')
)
BEGIN
    ALTER TABLE [revenue].[ExternalSalesRecord]
        ADD CONSTRAINT [FK_ExternalSalesRecord_ExternalPlatform]
            FOREIGN KEY ([ExternalPlatformId]) REFERENCES [revenue].[ExternalPlatform]([Id]);

    PRINT 'Added FK_ExternalSalesRecord_ExternalPlatform.';
END
ELSE
BEGIN
    PRINT 'FK_ExternalSalesRecord_ExternalPlatform already exists.';
END
GO

-- Index to support filtering imported records by platform
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ExternalSalesRecord_ExternalPlatform' AND [object_id] = OBJECT_ID('[revenue].[ExternalSalesRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExternalSalesRecord_ExternalPlatform]
        ON [revenue].[ExternalSalesRecord] ([BusinessId], [ExternalPlatformId])
        INCLUDE ([TransactionDate], [VatAmount], [TotalAmount], [IsActive]);

    PRINT 'Created IX_ExternalSalesRecord_ExternalPlatform.';
END
GO
