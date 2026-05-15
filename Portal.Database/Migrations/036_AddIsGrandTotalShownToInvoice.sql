/*
    Migration: 036_AddIsGrandTotalShownToInvoice
    Description: Adds a BIT column [IsGrandTotalShown] to [invoice].[Invoice].
                 When enabled, the invoice detail view renders the grand total card
                 showing per-section costs and overall totals (subtotal, discount, VAT, total).
                 Defaults to 1 (shown) for backward compatibility.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[Invoice]')
      AND name = N'IsGrandTotalShown'
)
BEGIN
    ALTER TABLE [invoice].[Invoice]
        ADD [IsGrandTotalShown] BIT NOT NULL CONSTRAINT DF_Invoice_IsGrandTotalShown DEFAULT 1;
END
GO
