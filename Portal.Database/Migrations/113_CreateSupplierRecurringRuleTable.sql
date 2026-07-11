-- ============================================================
-- Migration: 113_CreateSupplierRecurringRuleTable
-- Description: Creates the [billing].[SupplierRecurringRule] table for the
--              recurring expense validation feature. Business users define
--              expected purchase patterns per supplier (optionally scoped to
--              an expense category) which are validated against actual purchases.
--
-- Requirements: 1.1 - Table schema with all required columns
--               1.2 - Foreign key constraints to Business, Supplier, ExpenseCategory
--               1.3 - Non-clustered index on (BusinessId, SupplierId)
--               1.4 - Non-clustered index on BusinessId
--
-- This script is idempotent — safe to run multiple times.
-- ============================================================

USE [Portal]
GO

-- =============================================================================
-- 1. Create [billing].[SupplierRecurringRule]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'billing'
      AND TABLE_NAME = 'SupplierRecurringRule'
)
BEGIN
    CREATE TABLE [billing].[SupplierRecurringRule]
    (
        [Id]                      INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]              INT                           NOT NULL,
        [SupplierId]              INT                           NOT NULL,
        [ExpenseCategoryId]       INT                           NULL,
        [FrequencyMonths]         INT                           NOT NULL,
        [ExpectedAmount]          DECIMAL(18,2)                 NULL,
        [AmountTolerancePercent]  DECIMAL(5,2)                  NULL     CONSTRAINT [DF_SupplierRecurringRule_Tolerance] DEFAULT (5.00),
        [GracePeriodDays]         INT                           NOT NULL CONSTRAINT [DF_SupplierRecurringRule_GracePeriod] DEFAULT (0),
        [Description]             NVARCHAR(200)                 NOT NULL,
        [IsActive]                BIT                           NOT NULL CONSTRAINT [DF_SupplierRecurringRule_IsActive] DEFAULT (1),
        [IsDeleted]               BIT                           NOT NULL CONSTRAINT [DF_SupplierRecurringRule_IsDeleted] DEFAULT (0),
        [CreatedAtUtc]            DATETIME                      NOT NULL CONSTRAINT [DF_SupplierRecurringRule_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_SupplierRecurringRule] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_SupplierRecurringRule_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_SupplierRecurringRule_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [purchase].[Supplier] ([Id]),
        CONSTRAINT [FK_SupplierRecurringRule_ExpenseCategory] FOREIGN KEY ([ExpenseCategoryId]) REFERENCES [purchase].[ExpenseCategory] ([Id])
    );
END
GO

-- =============================================================================
-- 2. Non-clustered index on (BusinessId, SupplierId) for scoped rule lookups
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_SupplierRecurringRule_BusinessId_SupplierId'
      AND [object_id] = OBJECT_ID('[billing].[SupplierRecurringRule]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SupplierRecurringRule_BusinessId_SupplierId]
        ON [billing].[SupplierRecurringRule] ([BusinessId], [SupplierId]);
END
GO

-- =============================================================================
-- 3. Non-clustered index on BusinessId for tenant-filtered listing
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_SupplierRecurringRule_BusinessId'
      AND [object_id] = OBJECT_ID('[billing].[SupplierRecurringRule]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SupplierRecurringRule_BusinessId]
        ON [billing].[SupplierRecurringRule] ([BusinessId]);
END
GO
