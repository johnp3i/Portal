/*
    Migration: 035_AddIsGrandTotalShownToQuotation
    Description: Adds a BIT column [IsGrandTotalShown] to [quotation].[Quotation].
                 When enabled, the proposal snapshot renders the grand total card
                 showing per-section costs and overall totals (subtotal, discount, VAT, total).
                 Defaults to 1 (shown) for backward compatibility.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[Quotation]')
      AND name = N'IsGrandTotalShown'
)
BEGIN
    ALTER TABLE [quotation].[Quotation]
        ADD [IsGrandTotalShown] BIT NOT NULL CONSTRAINT DF_Quotation_IsGrandTotalShown DEFAULT 1;
END
GO
