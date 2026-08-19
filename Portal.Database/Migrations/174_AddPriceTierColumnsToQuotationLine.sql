-- ============================================================
-- Migration 174: Add Price Tier Columns to QuotationLine
-- ============================================================
-- Purpose: Adds ProductPriceTierId and PriceTierName columns to
--          [quotation].[QuotationLine] for price tier selection tracking.
--          Adds FK constraint referencing [product].[ProductPriceTier].
-- Schema: [quotation] (table), [product] (FK target)
-- ============================================================

USE [Portal]
GO

-- =============================================================================
-- 1. Add [ProductPriceTierId] column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'QuotationLine'
      AND COLUMN_NAME = 'ProductPriceTierId'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [ProductPriceTierId] INT NULL;
END
GO

-- =============================================================================
-- 2. Add [PriceTierName] column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'QuotationLine'
      AND COLUMN_NAME = 'PriceTierName'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [PriceTierName] NVARCHAR(100) NULL;
END
GO

-- =============================================================================
-- 3. Add FK constraint [FK_QuotationLine_ProductPriceTier]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_QuotationLine_ProductPriceTier'
      AND parent_object_id = OBJECT_ID('[quotation].[QuotationLine]')
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD CONSTRAINT [FK_QuotationLine_ProductPriceTier]
        FOREIGN KEY ([ProductPriceTierId])
        REFERENCES [product].[ProductPriceTier]([Id]);
END
GO
