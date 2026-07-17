-- ============================================================
-- Add Position column to Signature table
-- ============================================================

USE [Portal]
GO

ALTER TABLE [portal].[Signature]
    ADD [Position] NVARCHAR(100) NULL;
GO
