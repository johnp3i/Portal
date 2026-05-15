/*
    Migration: 031_AddProposalRenderingEnhancements
    Description: Adds columns to support proposal rendering enhancements:
                 - [quotation].[ProposalSection].SectionType: discriminator for "LineItems" vs "Narrative" sections (default 'LineItems')
                 - [quotation].[ProposalSection].IsEmphasized: flag for Signal Card emphasis pattern (default 0)
                 - [quotation].[ProposalSection].AccentColor: optional hex color for emphasized section left border
                 - [quotation].[QuotationLine].Subtitle: optional secondary description text below the line title

                 All new columns have safe defaults or are nullable — existing records retain current data.

    Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add [SectionType] to [quotation].[ProposalSection]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[ProposalSection]')
      AND name = N'SectionType'
)
BEGIN
    ALTER TABLE [quotation].[ProposalSection]
        ADD [SectionType] NVARCHAR(20) NOT NULL
            CONSTRAINT [DF_ProposalSection_SectionType] DEFAULT ('LineItems');
END
GO

-- =============================================================================
-- 2. Add [IsEmphasized] to [quotation].[ProposalSection]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[ProposalSection]')
      AND name = N'IsEmphasized'
)
BEGIN
    ALTER TABLE [quotation].[ProposalSection]
        ADD [IsEmphasized] BIT NOT NULL
            CONSTRAINT [DF_ProposalSection_IsEmphasized] DEFAULT (0);
END
GO

-- =============================================================================
-- 3. Add [AccentColor] to [quotation].[ProposalSection]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[ProposalSection]')
      AND name = N'AccentColor'
)
BEGIN
    ALTER TABLE [quotation].[ProposalSection]
        ADD [AccentColor] NVARCHAR(20) NULL;
END
GO

-- =============================================================================
-- 4. Add [Subtitle] to [quotation].[QuotationLine]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[QuotationLine]')
      AND name = N'Subtitle'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [Subtitle] NVARCHAR(1000) NULL;
END
GO
