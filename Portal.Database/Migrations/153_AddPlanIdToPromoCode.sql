-- ============================================================
-- Migration 153: Add PlanId to PromoCode table
-- ============================================================
-- Purpose: Adds a nullable PlanId column to [dbo].[PromoCode] so that
--          each promo code can specify which subscription tier it grants.
--          NULL = fallback to Professional tier (backward compat).
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'PromoCode' AND COLUMN_NAME = 'PlanId'
)
BEGIN
    ALTER TABLE [dbo].[PromoCode]
    ADD [PlanId] INT NULL;

    ALTER TABLE [dbo].[PromoCode]
    ADD CONSTRAINT [FK_PromoCode_Plan] FOREIGN KEY ([PlanId])
        REFERENCES [dbo].[Plan]([Id]);

    PRINT 'Added [PlanId] column to [dbo].[PromoCode] with FK to [dbo].[Plan].';
END
ELSE
BEGIN
    PRINT '[dbo].[PromoCode].[PlanId] already exists.';
END
GO
