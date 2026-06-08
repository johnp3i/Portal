/*
    Migration: 088_CreateExpenseCategoryLimitTable
    Description: Creates the [purchase].[ExpenseCategoryLimit] table — stores advisory
                 spending thresholds (annual and/or per-VAT-period) for each expense
                 category within a business. Limits are soft; they produce warnings but
                 never block purchase entry.

    Requirements: 1.1 - Table schema with Id, BusinessId, ExpenseCategoryId,
                         AnnualLimitEur, PeriodLimitEur, CreatedAtUtc
                  1.2 - Unique constraint on (BusinessId, ExpenseCategoryId)
                  1.3 - Foreign keys to [portal].[Business] and [purchase].[ExpenseCategory]
                  1.4 - Non-clustered index on BusinessId

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [purchase].[ExpenseCategoryLimit] table
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'ExpenseCategoryLimit'
)
BEGIN
    CREATE TABLE [purchase].[ExpenseCategoryLimit]
    (
        [Id]                  INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]          INT                           NOT NULL,
        [ExpenseCategoryId]   INT                           NOT NULL,
        [AnnualLimitEur]      DECIMAL(18,2)                 NULL,
        [PeriodLimitEur]      DECIMAL(18,2)                 NULL,
        [CreatedAtUtc]        DATETIME2                     NOT NULL  CONSTRAINT [DF_ExpenseCategoryLimit_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_ExpenseCategoryLimit] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ExpenseCategoryLimit_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_ExpenseCategoryLimit_ExpenseCategory] FOREIGN KEY ([ExpenseCategoryId]) REFERENCES [purchase].[ExpenseCategory] ([Id])
    );
END
GO

-- =============================================================================
-- 2. Unique constraint: one limit record per business per category
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'UX_ExpenseCategoryLimit_BusinessId_ExpenseCategoryId'
      AND [object_id] = OBJECT_ID('[purchase].[ExpenseCategoryLimit]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_ExpenseCategoryLimit_BusinessId_ExpenseCategoryId]
        ON [purchase].[ExpenseCategoryLimit] ([BusinessId], [ExpenseCategoryId]);
END
GO

-- =============================================================================
-- 3. Non-clustered index on BusinessId for tenant-filtered query optimisation
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_ExpenseCategoryLimit_BusinessId'
      AND [object_id] = OBJECT_ID('[purchase].[ExpenseCategoryLimit]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExpenseCategoryLimit_BusinessId]
        ON [purchase].[ExpenseCategoryLimit] ([BusinessId]);
END
GO
