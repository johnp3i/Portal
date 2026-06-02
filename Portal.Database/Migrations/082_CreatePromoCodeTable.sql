/*
    Migration: 082_CreatePromoCodeTable
    Description: Creates [dbo].[PromoCode] — a promotional code record that grants
                 prospects a free trial period on the Business plan without Stripe checkout.
                 Supports email-bound (single recipient) and generic (multi-use) modes.
                 Includes CHECK constraints for duration range, redemption limits, and
                 a UNIQUE constraint on the Code column.

    Requirements: 1.2  - PromoCode table with all required columns
                 1.4  - DurationMonths CHECK constraint between 1 and 24
                 1.5  - CurrentRedemptions CHECK constraint <= MaxRedemptions

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [dbo].[PromoCode]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'PromoCode'
)
BEGIN
    CREATE TABLE [dbo].[PromoCode]
    (
        [Id]                  INT             IDENTITY(1,1)   NOT NULL,
        [Code]                NVARCHAR(50)                    NOT NULL,
        [DurationMonths]      INT                             NOT NULL,
        [MaxRedemptions]      INT                             NOT NULL,
        [CurrentRedemptions]  INT                             NOT NULL  CONSTRAINT [DF_PromoCode_CurrentRedemptions] DEFAULT (0),
        [ExpiresAtUtc]        DATETIME                        NOT NULL,
        [BoundEmail]          NVARCHAR(256)                   NULL,
        [IsRevoked]           BIT                             NOT NULL  CONSTRAINT [DF_PromoCode_IsRevoked] DEFAULT (0),
        [CreatedByUserId]     NVARCHAR(450)                   NOT NULL,
        [CreatedAtUtc]        DATETIME                        NOT NULL  CONSTRAINT [DF_PromoCode_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_PromoCode] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UX_PromoCode_Code] UNIQUE ([Code]),
        CONSTRAINT [CK_PromoCode_DurationMonths] CHECK ([DurationMonths] >= 1 AND [DurationMonths] <= 24),
        CONSTRAINT [CK_PromoCode_MaxRedemptions] CHECK ([MaxRedemptions] > 0),
        CONSTRAINT [CK_PromoCode_CurrentRedemptions] CHECK ([CurrentRedemptions] >= 0 AND [CurrentRedemptions] <= [MaxRedemptions])
    );
END
GO

-- =============================================================================
-- 2. Create index on Code for fast lookup during validation
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_PromoCode_Code'
      AND [object_id] = OBJECT_ID('[dbo].[PromoCode]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PromoCode_Code]
        ON [dbo].[PromoCode] ([Code]);
END
GO

-- =============================================================================
-- 3. Create index on ExpiresAtUtc for expiry-based queries
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_PromoCode_ExpiresAtUtc'
      AND [object_id] = OBJECT_ID('[dbo].[PromoCode]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PromoCode_ExpiresAtUtc]
        ON [dbo].[PromoCode] ([ExpiresAtUtc]);
END
GO
