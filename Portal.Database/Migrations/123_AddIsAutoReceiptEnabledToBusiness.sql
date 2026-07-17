-- ============================================================
-- Add IsAutoReceiptEnabled column to Business table
-- ============================================================

USE [Portal]
GO

ALTER TABLE [portal].[Business]
    ADD [IsAutoReceiptEnabled] BIT NOT NULL CONSTRAINT [DF_Business_IsAutoReceiptEnabled] DEFAULT (0);
GO
