/*
    Migration: 089_AddIsDemoAccountToBusiness
    Description: Adds [IsDemoAccount] BIT column to [portal].[Business] to flag
                 businesses that serve as demo/showcase accounts. Includes a filtered
                 non-clustered index for fast lookup of demo businesses, and seeds
                 the existing demo business (Id = 1000) with the flag set to 1.

    Requirements: 1.1 - THE Portal_Database SHALL have a BIT column IsDemoAccount on [portal].[Business]
                  1.2 - THE Portal_Database SHALL index IsDemoAccount with a filtered index on IsDemoAccount = 1
                  1.3 - THE Portal_Database SHALL mark the existing demo business (Id = 1000) as IsDemoAccount = 1

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Add IsDemoAccount column with named default constraint
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[portal].[Business]')
      AND name = N'IsDemoAccount'
)
BEGIN
    ALTER TABLE [portal].[Business]
        ADD [IsDemoAccount] BIT NOT NULL
        CONSTRAINT [DF_Business_IsDemoAccount] DEFAULT (0);
END
GO

-- =============================================================================
-- 2. Create filtered non-clustered index on IsDemoAccount = 1
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[portal].[Business]')
      AND name = N'IX_Business_IsDemoAccount'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Business_IsDemoAccount]
        ON [portal].[Business] ([IsDemoAccount])
        WHERE [IsDemoAccount] = 1;
END
GO

-- =============================================================================
-- 3. Mark existing demo business (Id = 1000) as a demo account
-- =============================================================================

UPDATE [portal].[Business]
SET [IsDemoAccount] = 1
WHERE [Id] = 1000;
GO
