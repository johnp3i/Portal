-- Migration: Add VatRate column to QuotationLine table
-- Purpose: Support per-line VAT percentage for tax calculations

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[QuotationLine]')
      AND name = N'VatRate'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [VatRate] DECIMAL(5,2) NOT NULL CONSTRAINT [DF_QuotationLine_VatRate] DEFAULT 0;
END
GO
