-- ============================================================
-- Migration 147: Add ContactId FK to Customer
-- ============================================================
-- Purpose: Adds a nullable ContactId column to [customer].[Customer]
--          to link customers back to their originating sales contact
--          when converted via the pipeline "Mark as Won" flow.
-- Schema: [customer]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'customer' AND TABLE_NAME = 'Customer' AND COLUMN_NAME = 'ContactId'
)
BEGIN
    ALTER TABLE [customer].[Customer]
        ADD [ContactId] INT NULL;

    ALTER TABLE [customer].[Customer]
        ADD CONSTRAINT [FK_Customer_SalesContact]
        FOREIGN KEY ([ContactId]) REFERENCES [sales].[Contact]([Id]);

    PRINT 'Added [ContactId] column to [customer].[Customer].';
END
ELSE
BEGIN
    PRINT '[customer].[Customer].[ContactId] already exists.';
END
GO
