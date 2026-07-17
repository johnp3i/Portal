-- ============================================================
-- Add IsAutoInvoiceSignatureEnabled column to Business table
-- ============================================================

USE [Portal]
GO

ALTER TABLE [portal].[Business]
    ADD [IsAutoInvoiceSignatureEnabled] BIT NOT NULL CONSTRAINT [DF_Business_IsAutoInvoiceSignatureEnabled] DEFAULT (0);
GO
