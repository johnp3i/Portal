/*
    Migration: 028_AddDiscountTypeToQuotationLine
    Description: Adds DiscountType column to [quotation].[QuotationLine].
                 Values: 'Percentage' or 'Fixed'. Defaults to 'Percentage'.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[QuotationLine]')
      AND name = N'DiscountType'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [DiscountType] NVARCHAR(10) NOT NULL CONSTRAINT [DF_QuotationLine_DiscountType] DEFAULT (N'Percentage');
END
GO
