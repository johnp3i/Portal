/*
    Migration: 103_AddPaymentInstructionsOverrideToInvoice
    Description: Adds PaymentInstructionsOverride (TINYINT, nullable) to [invoice].[Invoice].
                 NULL = follow business default, 1 = force show, 0 = force hide.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[Invoice]')
      AND name = N'PaymentInstructionsOverride'
)
BEGIN
    ALTER TABLE [invoice].[Invoice]
        ADD [PaymentInstructionsOverride] TINYINT NULL;
END
GO
