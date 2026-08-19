/*
    Migration: 173_AddProductPriceTierIdToProductPriceHistory
    Description: Adds nullable ProductPriceTierId INT column to [product].[ProductPriceHistory]
                 with a FK constraint referencing [product].[ProductPriceTier](Id).
                 Enables price history entries to be linked to a specific price tier.
    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

-- =============================================================================
-- 1. Add [ProductPriceTierId] column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'product'
      AND TABLE_NAME = 'ProductPriceHistory'
      AND COLUMN_NAME = 'ProductPriceTierId'
)
BEGIN
    ALTER TABLE [product].[ProductPriceHistory]
        ADD [ProductPriceTierId] INT NULL;
END
GO

-- =============================================================================
-- 2. Add FK constraint referencing [product].[ProductPriceTier](Id)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_ProductPriceHistory_ProductPriceTier'
      AND parent_object_id = OBJECT_ID('[product].[ProductPriceHistory]')
)
BEGIN
    ALTER TABLE [product].[ProductPriceHistory]
        ADD CONSTRAINT [FK_ProductPriceHistory_ProductPriceTier]
            FOREIGN KEY ([ProductPriceTierId])
            REFERENCES [product].[ProductPriceTier]([Id]);
END
GO
