/*
    Migration: 032_AddLabelToProposalSection
    Description: Adds a nullable [Label] column to [quotation].[ProposalSection].
                 This is the small eyebrow text displayed above the section title in the proposal snapshot.
                 Defaults: NULL (renderer falls back to "Content" for Narrative, ColumnConfiguration for LineItems).

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[ProposalSection]')
      AND name = N'Label'
)
BEGIN
    ALTER TABLE [quotation].[ProposalSection]
        ADD [Label] NVARCHAR(50) NULL;
END
GO
