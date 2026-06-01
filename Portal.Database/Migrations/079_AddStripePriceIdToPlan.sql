/*
    Migration: 079_AddStripePriceIdToPlan
    Description: Adds StripePriceId column to [dbo].[Plan] to link each plan
                 to its corresponding Stripe Price object for checkout session creation.

    Requirements: 6.6 - StripePriceId (NVARCHAR(100), nullable)

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add StripePriceId column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Plan]')
      AND name = N'StripePriceId'
)
BEGIN
    ALTER TABLE [dbo].[Plan]
        ADD [StripePriceId] NVARCHAR(100) NULL;
END
GO
