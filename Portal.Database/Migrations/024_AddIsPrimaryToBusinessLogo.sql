/*
    Migration: 024_AddIsPrimaryToBusinessLogo
    Description: Adds IsPrimary flag to [portal].[BusinessLogo] to distinguish the primary
                 business logo from product/partner logos. Only one logo per business can be primary.

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add IsPrimary column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[portal].[BusinessLogo]')
      AND name = N'IsPrimary'
)
BEGIN
    ALTER TABLE [portal].[BusinessLogo]
        ADD [IsPrimary] BIT NOT NULL CONSTRAINT [DF_BusinessLogo_IsPrimary] DEFAULT (0);
END
GO
