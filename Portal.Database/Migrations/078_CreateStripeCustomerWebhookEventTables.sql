USE [Portal];
GO

/*
    Migration: 078_CreateStripeCustomerWebhookEventTables
    Description: Creates the [stripe].[Customer] table mapping Portal BusinessId to
                 Stripe Customer Id, and the [stripe].[WebhookEvent] table for webhook
                 idempotency (preventing duplicate event processing).

    Requirements: 6.4  - stripe.Customer table with Id, BusinessId, StripeCustomerId, CreatedAtUtc
                 6.5  - stripe.WebhookEvent table with Id, EventId, Type, ProcessedAtUtc, CreatedAtUtc
                 6.10 - FK from stripe.Customer.BusinessId to [portal].[Business].Id
                 6.13 - Tables reside in [stripe] schema (created in migration 076)

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [stripe].[Customer]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'stripe'
      AND TABLE_NAME = 'Customer'
)
BEGIN
    CREATE TABLE [stripe].[Customer]
    (
        [Id]               INT            IDENTITY(1,1)   NOT NULL,
        [BusinessId]       INT                            NOT NULL,
        [StripeCustomerId] NVARCHAR(100)                  NOT NULL,
        [CreatedAtUtc]     DATETIME                       NOT NULL  CONSTRAINT [DF_StripeCustomer_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_StripeCustomer] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_StripeCustomer_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [UX_StripeCustomer_StripeCustomerId] UNIQUE ([StripeCustomerId])
    );
END
GO

-- =============================================================================
-- 2. Nonclustered index on BusinessId (FK column)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_StripeCustomer_BusinessId'
      AND [object_id] = OBJECT_ID('[stripe].[Customer]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_StripeCustomer_BusinessId]
        ON [stripe].[Customer] ([BusinessId]);
END
GO

-- =============================================================================
-- 3. Create [stripe].[WebhookEvent]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'stripe'
      AND TABLE_NAME = 'WebhookEvent'
)
BEGIN
    CREATE TABLE [stripe].[WebhookEvent]
    (
        [Id]             INT            IDENTITY(1,1)   NOT NULL,
        [EventId]        NVARCHAR(100)                  NOT NULL,
        [Type]           NVARCHAR(100)                  NOT NULL,
        [ProcessedAtUtc] DATETIME                       NULL,
        [CreatedAtUtc]   DATETIME                       NOT NULL  CONSTRAINT [DF_WebhookEvent_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_WebhookEvent] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UX_WebhookEvent_EventId] UNIQUE ([EventId])
    );
END
GO
