/*
    Migration: 041_AddIsQuotationReferenceShownToInvoice
    Description: Adds [IsQuotationReferenceShown] BIT column to [invoice].[Invoice].
                 Controls whether the source quotation reference is displayed on the
                 invoice preview. Defaults to 1 (shown).

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[Invoice]')
      AND name = N'IsQuotationReferenceShown'
)
BEGIN
    ALTER TABLE [invoice].[Invoice]
        ADD [IsQuotationReferenceShown] BIT NOT NULL CONSTRAINT DF_Invoice_IsQuotationReferenceShown DEFAULT 1;
END
GO
