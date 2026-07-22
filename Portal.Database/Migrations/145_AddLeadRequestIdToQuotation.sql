-- ============================================================
-- Migration 145: Add LeadRequestId FK to Quotation
-- ============================================================
-- Purpose: Adds a nullable LeadRequestId column to [quotation].[Quotation]
--          to support linking quotations/proposals back to sales pipeline leads.
-- Schema: [quotation]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'quotation' AND TABLE_NAME = 'Quotation' AND COLUMN_NAME = 'LeadRequestId'
)
BEGIN
    ALTER TABLE [quotation].[Quotation]
        ADD [LeadRequestId] INT NULL;

    ALTER TABLE [quotation].[Quotation]
        ADD CONSTRAINT [FK_Quotation_LeadRequest]
        FOREIGN KEY ([LeadRequestId]) REFERENCES [sales].[LeadRequest]([Id]);

    PRINT 'Added [LeadRequestId] column to [quotation].[Quotation].';
END
ELSE
BEGIN
    PRINT '[quotation].[Quotation].[LeadRequestId] already exists.';
END
GO
