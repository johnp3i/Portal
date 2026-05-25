/*
    Migration: 052_CreateProductTable
    Description: Creates the [product].Product table — a master catalog record representing
                 a sellable item or service, scoped to a business tenant. Includes pricing,
                 supplier association, and usage tracking.

    Requirements: 1.2 - THE Portal_Database SHALL contain a [product].[Product] table with columns:
                         Id (PK, int identity), BusinessId (FK to [portal].Business, required),
                         ProductCode (nvarchar(50), required), Description (nvarchar(500), required),
                         DefaultSellingPrice (decimal(18,2), required, minimum value 0.00),
                         DefaultCostPrice (decimal(18,2), required, minimum value 0.00),
                         DefaultVatRate (decimal(5,2), required, range 0.00 to 99.99),
                         SupplierId (FK to [purchase].Supplier, nullable), IsActive (bit, default 1),
                         LastUsedDate (datetime2, nullable), CreatedAtUtc (datetime2, default GETUTCDATE())
                 1.3 - THE Portal_Database SHALL enforce a unique constraint on the combination
                         of BusinessId and ProductCode in the [product].[Product] table
                 1.9 - THE Portal_Database SHALL create a nonclustered index on [product].[Product]
                         for BusinessId to optimise tenant-scoped queries
                 1.10 - THE Portal_Database SHALL create a nonclustered index on [product].[Product]
                         for the combination of BusinessId and ProductCode to optimise autocomplete lookups

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'product'
      AND TABLE_NAME = 'Product'
)
BEGIN
    CREATE TABLE [product].[Product]
    (
        [Id]                   INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]           INT                           NOT NULL,
        [ProductCode]          NVARCHAR(50)                  NOT NULL,
        [Description]          NVARCHAR(500)                 NOT NULL,
        [DefaultSellingPrice]  DECIMAL(18,2)                 NOT NULL,
        [DefaultCostPrice]     DECIMAL(18,2)                 NOT NULL,
        [DefaultVatRate]       DECIMAL(5,2)                  NOT NULL,
        [SupplierId]           INT                           NULL,
        [IsActive]             BIT                           NOT NULL  CONSTRAINT [DF_Product_IsActive] DEFAULT (1),
        [LastUsedDate]         DATETIME2                     NULL,
        [CreatedAtUtc]         DATETIME2                     NOT NULL  CONSTRAINT [DF_Product_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Product] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Product_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_Product_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [purchase].[Supplier] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [UQ_Product_BusinessId_ProductCode] UNIQUE ([BusinessId], [ProductCode]),
        CONSTRAINT [CK_Product_DefaultSellingPrice] CHECK ([DefaultSellingPrice] >= 0),
        CONSTRAINT [CK_Product_DefaultCostPrice] CHECK ([DefaultCostPrice] >= 0),
        CONSTRAINT [CK_Product_DefaultVatRate] CHECK ([DefaultVatRate] BETWEEN 0.00 AND 99.99)
    );
END
GO

-- Nonclustered index on BusinessId for tenant-scoped query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Product_BusinessId'
      AND [object_id] = OBJECT_ID('[product].[Product]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Product_BusinessId]
        ON [product].[Product] ([BusinessId]);
END
GO

-- Nonclustered index on (BusinessId, ProductCode) for autocomplete lookup optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Product_BusinessId_ProductCode'
      AND [object_id] = OBJECT_ID('[product].[Product]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Product_BusinessId_ProductCode]
        ON [product].[Product] ([BusinessId], [ProductCode]);
END
GO
