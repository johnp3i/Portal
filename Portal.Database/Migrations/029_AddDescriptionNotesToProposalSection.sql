/*
    Migration: 029_AddDescriptionNotesToProposalSection
    Description: Adds Description and Notes columns to [quotation].[ProposalSection].
                 - Description: optional text displayed below section heading (max 2000 chars)
                 - Notes: optional text displayed below line items table (max 4000 chars)
                 Both columns are nullable — existing records will have NULL.

    Requirements: 6.1, 6.2, 6.4

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[ProposalSection]')
      AND name = N'Description'
)
BEGIN
    ALTER TABLE [quotation].[ProposalSection]
        ADD [Description] NVARCHAR(2000) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[ProposalSection]')
      AND name = N'Notes'
)
BEGIN
    ALTER TABLE [quotation].[ProposalSection]
        ADD [Notes] NVARCHAR(4000) NULL;
END
GO
