/*
    Migration: 172_CreateProductPriceTierTable
    Description: Creates the [product].[ProductPriceTier] table — a named pricing level
                 for a product (e.g., Retail, Wholesale, VIP). Each tier holds its own
                 SellingPrice and CostPrice. Products may have zero or more tiers.
                 Includes filtered unique index for active tier name uniqueness and
                 a covering index for frequent active-tier queries.

    Requirements: 1.1, 1.2, 2.2, 3.1

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

-- =============================================================================
-- 1. Create [product].[ProductPriceTier] table
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'product'
      AND TABLE_NAME = 'ProductPriceTier'
)
BEGIN
    CREATE TABLE [product].[ProductPriceTier]
    (
        [Id]                INT            IDENTITY(1,1)  NOT NULL,
        [ProductId]         INT                           NOT NULL,
        [TierName]          NVARCHAR(100)                 NOT NULL,
        [SellingPrice]      DECIMAL(18,2)                 NOT NULL,
        [CostPrice]         DECIMAL(18,2)                 NOT NULL,
        [IsDefault]         BIT                           NOT NULL  CONSTRAINT [DF_ProductPriceTier_IsDefault] DEFAULT (0),
        [IsActive]          BIT                           NOT NULL  CONSTRAINT [DF_ProductPriceTier_IsActive] DEFAULT (1),
        [CreatedAtUtc]      DATETIME                      NOT NULL  CONSTRAINT [DF_ProductPriceTier_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]      DATETIME                      NOT NULL  CONSTRAINT [DF_ProductPriceTier_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_ProductPriceTier] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ProductPriceTier_Product] FOREIGN KEY ([ProductId]) REFERENCES [product].[Product] ([Id])
    );
END
GO

-- =============================================================================
-- 2. Filtered unique index: ensures tier name uniqueness among active tiers only
--    Allows reuse of a deactivated tier's name for a new active tier.
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'UQ_ProductPriceTier_ActiveName'
      AND [object_id] = OBJECT_ID('[product].[ProductPriceTier]')
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ProductPriceTier_ActiveName]
        ON [product].[ProductPriceTier] ([ProductId], [TierName])
        WHERE [IsActive] = 1;
END
GO

-- =============================================================================
-- 3. Covering index for frequent active-tier queries (tier selector dropdown)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_ProductPriceTier_ProductId_IsActive'
      AND [object_id] = OBJECT_ID('[product].[ProductPriceTier]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ProductPriceTier_ProductId_IsActive]
        ON [product].[ProductPriceTier] ([ProductId], [IsActive])
        INCLUDE ([TierName], [SellingPrice], [CostPrice], [IsDefault]);
END
GO
