/*
    Migration: 087_AddStripeProductIdToPlan
    Description: Adds StripeProductId column to [dbo].[Plan] to link each plan
                 to its corresponding Stripe Product object for reference and reconciliation.

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add StripeProductId column
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Plan]')
      AND name = N'StripeProductId'
)
BEGIN
    ALTER TABLE [dbo].[Plan]
        ADD [StripeProductId] NVARCHAR(100) NULL;
END
GO
