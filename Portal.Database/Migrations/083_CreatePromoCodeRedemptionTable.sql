/*
    Migration: 083_CreatePromoCodeRedemptionTable
    Description: Creates [dbo].[PromoCodeRedemption] — tracks which user and business
                 redeemed a specific promo code and when. Each record represents a single
                 successful promo code redemption during the provisioning flow.

    Requirements: 1.3  - PromoCodeRedemption table with all required columns
                 1.6  - FK from PromoCodeId to [dbo].[PromoCode].Id and from
                         BusinessId to [portal].[Business].Id

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [dbo].[PromoCodeRedemption]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'PromoCodeRedemption'
)
BEGIN
    CREATE TABLE [dbo].[PromoCodeRedemption]
    (
        [Id]             INT             IDENTITY(1,1)   NOT NULL,
        [PromoCodeId]    INT                             NOT NULL,
        [UserId]         NVARCHAR(450)                   NOT NULL,
        [BusinessId]     INT                             NOT NULL,
        [RedeemedAtUtc]  DATETIME                        NOT NULL,
        [CreatedAtUtc]   DATETIME                        NOT NULL  CONSTRAINT [DF_PromoCodeRedemption_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_PromoCodeRedemption] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PromoCodeRedemption_PromoCode] FOREIGN KEY ([PromoCodeId]) REFERENCES [dbo].[PromoCode] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromoCodeRedemption_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- =============================================================================
-- 2. Nonclustered index on PromoCodeId (FK column, supports join queries)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_PromoCodeRedemption_PromoCodeId'
      AND [object_id] = OBJECT_ID('[dbo].[PromoCodeRedemption]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PromoCodeRedemption_PromoCodeId]
        ON [dbo].[PromoCodeRedemption] ([PromoCodeId]);
END
GO

-- =============================================================================
-- 3. Nonclustered index on BusinessId (FK column)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_PromoCodeRedemption_BusinessId'
      AND [object_id] = OBJECT_ID('[dbo].[PromoCodeRedemption]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PromoCodeRedemption_BusinessId]
        ON [dbo].[PromoCodeRedemption] ([BusinessId]);
END
GO
