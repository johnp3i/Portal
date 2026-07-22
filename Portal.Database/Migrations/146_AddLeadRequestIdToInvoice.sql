-- ============================================================
-- Migration 146: Add LeadRequestId FK to Invoice
-- ============================================================
-- Purpose: Adds a nullable LeadRequestId column to [invoice].[Invoice]
--          to support linking invoices back to sales pipeline leads.
-- Schema: [invoice]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'invoice' AND TABLE_NAME = 'Invoice' AND COLUMN_NAME = 'LeadRequestId'
)
BEGIN
    ALTER TABLE [invoice].[Invoice]
        ADD [LeadRequestId] INT NULL;

    ALTER TABLE [invoice].[Invoice]
        ADD CONSTRAINT [FK_Invoice_LeadRequest]
        FOREIGN KEY ([LeadRequestId]) REFERENCES [sales].[LeadRequest]([Id]);

    PRINT 'Added [LeadRequestId] column to [invoice].[Invoice].';
END
ELSE
BEGIN
    PRINT '[invoice].[Invoice].[LeadRequestId] already exists.';
END
GO
