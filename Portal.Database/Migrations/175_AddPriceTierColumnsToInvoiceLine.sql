-- ============================================================
-- Migration 175: Add Price Tier Columns to InvoiceLine
-- ============================================================
-- Purpose: Adds ProductPriceTierId and PriceTierName columns to
--          [invoice].[InvoiceLine] for price tier selection tracking.
--          Adds FK constraint referencing [product].[ProductPriceTier].
-- Schema: [invoice] (table), [product] (FK target)
-- ============================================================

USE [Portal]
GO

-- =============================================================================
-- 1. Add [ProductPriceTierId] column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceLine'
      AND COLUMN_NAME = 'ProductPriceTierId'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [ProductPriceTierId] INT NULL;
END
GO

-- =============================================================================
-- 2. Add [PriceTierName] column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceLine'
      AND COLUMN_NAME = 'PriceTierName'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [PriceTierName] NVARCHAR(100) NULL;
END
GO

-- =============================================================================
-- 3. Add FK constraint [FK_InvoiceLine_ProductPriceTier]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_InvoiceLine_ProductPriceTier'
      AND parent_object_id = OBJECT_ID('[invoice].[InvoiceLine]')
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD CONSTRAINT [FK_InvoiceLine_ProductPriceTier]
        FOREIGN KEY ([ProductPriceTierId])
        REFERENCES [product].[ProductPriceTier]([Id]);
END
GO
