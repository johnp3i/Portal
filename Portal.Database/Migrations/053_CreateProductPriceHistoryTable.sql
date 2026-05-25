/*
    Migration: 053_CreateProductPriceHistoryTable
    Description: Creates the [product].ProductPriceHistory table — an append-only log
                 capturing each change to a product's selling or cost price, with the
                 effective date and the user who made the change.

    Requirements: 1.4 - THE Portal_Database SHALL contain a [product].[ProductPriceHistory] table
                         with columns: Id (PK, int identity), ProductId (FK to [product].[Product]),
                         SellingPrice (decimal(18,2), required, minimum value 0.00),
                         CostPrice (decimal(18,2), required, minimum value 0.00),
                         EffectiveFromUtc (datetime2, required),
                         ChangedByUserId (nvarchar(450), required),
                         CreatedAtUtc (datetime2, default GETUTCDATE())
                 1.5 - THE Portal_Database SHALL enforce cascading delete from
                         [product].[Product] to [product].[ProductPriceHistory]
                 1.11 - THE Portal_Database SHALL create a nonclustered index on
                         [product].[ProductPriceHistory] for ProductId to optimise
                         price history retrieval

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'product'
      AND TABLE_NAME = 'ProductPriceHistory'
)
BEGIN
    CREATE TABLE [product].[ProductPriceHistory]
    (
        [Id]                INT            IDENTITY(1,1)  NOT NULL,
        [ProductId]         INT                           NOT NULL,
        [SellingPrice]      DECIMAL(18,2)                 NOT NULL,
        [CostPrice]         DECIMAL(18,2)                 NOT NULL,
        [EffectiveFromUtc]  DATETIME2                     NOT NULL,
        [ChangedByUserId]   NVARCHAR(450)                 NOT NULL,
        [CreatedAtUtc]      DATETIME2                     NOT NULL  CONSTRAINT [DF_ProductPriceHistory_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_ProductPriceHistory] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ProductPriceHistory_Product] FOREIGN KEY ([ProductId]) REFERENCES [product].[Product] ([Id]) ON DELETE CASCADE
    );
END
GO

-- Non-clustered index on ProductId for price history retrieval optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_ProductPriceHistory_ProductId'
      AND [object_id] = OBJECT_ID('[product].[ProductPriceHistory]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ProductPriceHistory_ProductId]
        ON [product].[ProductPriceHistory] ([ProductId]);
END
GO
