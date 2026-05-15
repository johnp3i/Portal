/*
    Migration: 026_AddDiscountToQuotationLine
    Description: Adds a Discount percentage column to [quotation].[QuotationLine].
                 Discount is applied as a percentage (0-100) to the line total calculation.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[QuotationLine]')
      AND name = N'Discount'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [Discount] DECIMAL(5,2) NOT NULL CONSTRAINT [DF_QuotationLine_Discount] DEFAULT (0);
END
GO
