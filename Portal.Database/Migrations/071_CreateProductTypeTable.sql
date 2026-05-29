/*
    Migration: 071_CreateProductTypeTable
    Description: Creates the [product].[ProductType] lookup table — a system-wide
                 reference table classifying whether a product relates to
                 Services or Goods.
                 
                 Also adds a nullable ProductTypeId FK column to [product].[Product]
                 to associate each product with a product type. Nullable to allow
                 backward compatibility with products created before this feature.

    Requirements: 1.1 - THE Portal_System SHALL provide a ProductType lookup table with
                         two entries: Services (Id=1) and Goods (Id=2)
                  1.2 - THE Portal_System SHALL enforce that the ProductType table accepts
                         only manually seeded Id values (no IDENTITY)
                  2.1 - THE Portal_System SHALL store a nullable ProductTypeId foreign key
                         on the Product table referencing the ProductType lookup
                  8.3 - THE Portal_System SHALL accept ProductTypeId values of NULL, 1, or 2
                  8.4 - THE migration SHALL be idempotent (safe to run multiple times)

    This script is idempotent — safe to run multiple times.
*/

-- Step 1: Create the ProductType lookup table
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'product'
      AND TABLE_NAME = 'ProductType'
)
BEGIN
    CREATE TABLE [product].[ProductType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_ProductType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Step 2: Seed data — idempotent inserts
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'product'
      AND TABLE_NAME = 'ProductType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [product].[ProductType] WHERE [Id] = 1)
        INSERT INTO [product].[ProductType] ([Id], [Name]) VALUES (1, 'Services');

    IF NOT EXISTS (SELECT 1 FROM [product].[ProductType] WHERE [Id] = 2)
        INSERT INTO [product].[ProductType] ([Id], [Name]) VALUES (2, 'Goods');
END
GO

-- Step 3: Add ProductTypeId column to [product].[Product] if not already present
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'product'
      AND TABLE_NAME = 'Product'
      AND COLUMN_NAME = 'ProductTypeId'
)
BEGIN
    ALTER TABLE [product].[Product]
        ADD [ProductTypeId] INT NULL;
END
GO

-- Step 4: Add FK constraint from Product to ProductType
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_Product_ProductType'
      AND [parent_object_id] = OBJECT_ID('[product].[Product]')
)
BEGIN
    ALTER TABLE [product].[Product]
        ADD CONSTRAINT [FK_Product_ProductType]
            FOREIGN KEY ([ProductTypeId])
            REFERENCES [product].[ProductType] ([Id]);
END
GO
