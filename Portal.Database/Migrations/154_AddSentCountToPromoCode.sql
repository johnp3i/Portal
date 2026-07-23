-- ============================================================
-- Migration 154: Add SentCount to PromoCode table
-- ============================================================
-- Purpose: Tracks how many times a promo code has been emailed.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'PromoCode' AND COLUMN_NAME = 'SentCount'
)
BEGIN
    ALTER TABLE [dbo].[PromoCode]
    ADD [SentCount] INT NOT NULL CONSTRAINT [DF_PromoCode_SentCount] DEFAULT (0);

    PRINT 'Added [SentCount] column to [dbo].[PromoCode].';
END
ELSE
BEGIN
    PRINT '[dbo].[PromoCode].[SentCount] already exists.';
END
GO
