/*
    Migration: 033_AddCostPriceToQuotationLine
    Description: Adds CostPrice column to [quotation].[QuotationLine].
                 Stores the actual purchase/cost price of a line item for internal
                 profit/margin tracking. Nullable — not all items have a known cost.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[QuotationLine]')
      AND name = N'CostPrice'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [CostPrice] DECIMAL(18,2) NULL;
END
GO
