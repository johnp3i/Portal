USE [Portal];
GO

/*
    Migration: 076_CreateBillingStripeSchemas
    Description: Creates the [billing] and [stripe] schemas, then creates the
                 [billing].[Subscription] table — the single source of truth for
                 a tenant's subscription lifecycle. Includes foreign keys to
                 [portal].[Business] and [dbo].[Plan], a CHECK constraint on Status,
                 and a StripeSubscriptionId column for Stripe correlation.

    Requirements: 6.1  - Subscription table in [billing] schema with all required columns
                 6.7  - FK from BusinessId to [portal].[Business].Id and PlanId to [dbo].[Plan].Id
                 6.11 - Status CHECK constraint: active, past_due, cancelled, trialing, incomplete, unpaid
                 6.13 - [billing] and [stripe] schemas created with IF NOT EXISTS guards

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [billing] schema
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'billing')
BEGIN
    EXEC('CREATE SCHEMA [billing]');
END
GO

-- =============================================================================
-- 2. Create [stripe] schema
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'stripe')
BEGIN
    EXEC('CREATE SCHEMA [stripe]');
END
GO

-- =============================================================================
-- 3. Create [billing].[Subscription]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'billing'
      AND TABLE_NAME = 'Subscription'
)
BEGIN
    CREATE TABLE [billing].[Subscription]
    (
        [Id]                     INT            IDENTITY(1,1)   NOT NULL,
        [BusinessId]             INT                            NOT NULL,
        [PlanId]                 INT                            NOT NULL,
        [Status]                 NVARCHAR(20)                   NOT NULL,
        [StripeSubscriptionId]   NVARCHAR(100)                  NULL,
        [CurrentPeriodStart]     DATETIME                       NOT NULL,
        [CurrentPeriodEnd]       DATETIME                       NOT NULL,
        [CancelledAtUtc]         DATETIME                       NULL,
        [CreatedAtUtc]           DATETIME                       NOT NULL  CONSTRAINT [DF_Subscription_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Subscription] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Subscription_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Subscription_Plan] FOREIGN KEY ([PlanId]) REFERENCES [dbo].[Plan] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [CK_Subscription_Status] CHECK ([Status] IN ('active','past_due','cancelled','trialing','incomplete','unpaid'))
    );
END
GO

-- =============================================================================
-- 4. Nonclustered index on BusinessId (FK column)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Subscription_BusinessId'
      AND [object_id] = OBJECT_ID('[billing].[Subscription]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Subscription_BusinessId]
        ON [billing].[Subscription] ([BusinessId]);
END
GO

-- =============================================================================
-- 5. Nonclustered index on PlanId (FK column)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Subscription_PlanId'
      AND [object_id] = OBJECT_ID('[billing].[Subscription]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Subscription_PlanId]
        ON [billing].[Subscription] ([PlanId]);
END
GO
