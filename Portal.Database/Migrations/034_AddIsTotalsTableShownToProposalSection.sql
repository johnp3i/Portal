/*
    Migration: 034_AddIsTotalsTableShownToProposalSection
    Description: Adds a BIT column [IsTotalsTableShown] to [quotation].[ProposalSection].
                 When enabled, the proposal snapshot renders a detailed totals breakdown
                 (subtotal, discount, VAT, total) for that section.
                 Defaults to 0 (not shown).

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[ProposalSection]')
      AND name = N'IsTotalsTableShown'
)
BEGIN
    ALTER TABLE [quotation].[ProposalSection]
        ADD [IsTotalsTableShown] BIT NOT NULL CONSTRAINT DF_ProposalSection_IsTotalsTableShown DEFAULT 0;
END
GO
