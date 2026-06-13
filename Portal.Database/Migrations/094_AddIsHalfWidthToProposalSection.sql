-- Migration 094: Add IsHalfWidth column to ProposalSection
-- Allows narrative sections to render side-by-side at half width in the proposal

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[quotation].[ProposalSection]')
    AND name = 'IsHalfWidth'
)
BEGIN
    ALTER TABLE [quotation].[ProposalSection]
    ADD [IsHalfWidth] BIT NOT NULL CONSTRAINT DF_ProposalSection_IsHalfWidth DEFAULT 0;
END
GO
