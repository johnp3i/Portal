/*
    Migration: 086_AddInvoiceNumberToBillingInvoice
    Description: Adds InvoiceNumber (NVARCHAR(50), NULL) and IsEmailSent (BIT, NOT NULL, DEFAULT 0)
                 columns to [billing].[Invoice]. Creates a filtered unique nonclustered index
                 on InvoiceNumber that applies only to non-null values, allowing existing records
                 to retain NULL without constraint violation.

    Requirements: 3.1, 3.2, 6.7

    This script is idempotent — safe to run multiple times.
*/

-- Add InvoiceNumber column (nullable to support existing records without retroactive assignment)
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('[billing].[Invoice]')
      AND [name] = 'InvoiceNumber'
)
BEGIN
    ALTER TABLE [billing].[Invoice]
        ADD [InvoiceNumber] NVARCHAR(50) NULL;
END
GO

-- Add IsEmailSent column (tracks whether the notification email has been sent)
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('[billing].[Invoice]')
      AND [name] = 'IsEmailSent'
)
BEGIN
    ALTER TABLE [billing].[Invoice]
        ADD [IsEmailSent] BIT NOT NULL CONSTRAINT [DF_Invoice_IsEmailSent] DEFAULT (0);
END
GO

-- Filtered unique index: only applies to non-null InvoiceNumber values
-- Allows multiple NULL records while enforcing uniqueness on assigned numbers
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'UX_Invoice_InvoiceNumber'
      AND [object_id] = OBJECT_ID('[billing].[Invoice]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Invoice_InvoiceNumber]
        ON [billing].[Invoice] ([InvoiceNumber])
        WHERE [InvoiceNumber] IS NOT NULL;
END
GO
