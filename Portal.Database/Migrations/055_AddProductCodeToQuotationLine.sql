/*
    Migration: 055_AddProductCodeToQuotationLine
    Description: Adds a nullable ProductCode column to [quotation].[QuotationLine] to support
                 linking quotation line items to the product catalog via product code.

    Requirements: 1.8 - THE Portal_Database SHALL add a nullable ProductCode column (nvarchar(50))
                        to the [quotation].[QuotationLine] table

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[QuotationLine]')
      AND name = N'ProductCode'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [ProductCode] NVARCHAR(50) NULL;
END
GO
