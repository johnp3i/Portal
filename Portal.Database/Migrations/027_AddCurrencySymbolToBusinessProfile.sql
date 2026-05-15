/*
    Migration: 027_AddCurrencySymbolToBusinessProfile
    Description: Adds CurrencySymbol column to [portal].[BusinessProfile].
                 Defaults to '€' (Euro). Used for formatting monetary values in quotations and proposals.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[portal].[BusinessProfile]')
      AND name = N'CurrencySymbol'
)
BEGIN
    ALTER TABLE [portal].[BusinessProfile]
        ADD [CurrencySymbol] NVARCHAR(5) NOT NULL CONSTRAINT [DF_BusinessProfile_CurrencySymbol] DEFAULT (N'€');
END
GO
