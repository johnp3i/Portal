-- ============================================================
-- Migration 169: Add LeadPriorityTypeId FK to LeadRequest
-- ============================================================
-- Purpose: Adds a nullable LeadPriorityTypeId column to [sales].[LeadRequest]
--          to support assigning priority levels (Hot/Warm/Cold) to leads.
-- Schema: [sales]
-- ============================================================

USE Portal
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadRequest' AND COLUMN_NAME = 'LeadPriorityTypeId'
)
BEGIN
    ALTER TABLE [sales].[LeadRequest]
        ADD [LeadPriorityTypeId] INT NULL;

    ALTER TABLE [sales].[LeadRequest]
        ADD CONSTRAINT [FK_LeadRequest_LeadPriorityType]
        FOREIGN KEY ([LeadPriorityTypeId]) REFERENCES [sales].[LeadPriorityType]([Id]);

    PRINT 'Added [LeadPriorityTypeId] column to [sales].[LeadRequest].';
END
ELSE
BEGIN
    PRINT '[sales].[LeadRequest].[LeadPriorityTypeId] already exists.';
END
GO
