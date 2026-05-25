/*
    Migration: 054_AddProductCodeToInvoiceLine
    Description: Adds a nullable ProductCode column to [invoice].[InvoiceLine] to support
                 linking invoice line items to the product catalog via product code.

    Requirements: 1.7 - THE Portal_Database SHALL add a nullable ProductCode column (nvarchar(50))
                        to the [invoice].[InvoiceLine] table

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[InvoiceLine]')
      AND name = N'ProductCode'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [ProductCode] NVARCHAR(50) NULL;
END
GO
