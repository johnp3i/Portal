/*
    Migration: 068_CreatePurchaseTypeTable
    Description: Creates the [purchase].[PurchaseType] lookup table — a system-wide
                 reference table classifying whether a purchase is an Asset, Stock,
                 or Expense. This is a shared reference table with no BusinessId column.

                 Also adds a PurchaseTypeId column (NOT NULL, DEFAULT 3) to
                 [purchase].[Purchase] with a FK constraint to [purchase].[PurchaseType].
                 Existing purchases are automatically assigned PurchaseTypeId=3 (Expense)
                 via the default constraint.

    Requirements: 3.1 - THE Portal_System SHALL provide a PurchaseType lookup table
                         with three entries: Asset (Id=1), Stock (Id=2), Expense (Id=3)
                 3.2 - THE Portal_System SHALL store a PurchaseTypeId column (NOT NULL)
                         on the Purchase table referencing the PurchaseType lookup
                 3.8 - THE Portal_System SHALL default the PurchaseTypeId to Expense (Id=3)
                         for existing purchases that predate this feature

    This script is idempotent — safe to run multiple times.
*/

-- Step 1: Create the PurchaseType lookup table
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'PurchaseType'
)
BEGIN
    CREATE TABLE [purchase].[PurchaseType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_PurchaseType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Step 2: Seed data — idempotent inserts
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'PurchaseType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [purchase].[PurchaseType] WHERE [Id] = 1)
        INSERT INTO [purchase].[PurchaseType] ([Id], [Name]) VALUES (1, 'Asset');

    IF NOT EXISTS (SELECT 1 FROM [purchase].[PurchaseType] WHERE [Id] = 2)
        INSERT INTO [purchase].[PurchaseType] ([Id], [Name]) VALUES (2, 'Stock');

    IF NOT EXISTS (SELECT 1 FROM [purchase].[PurchaseType] WHERE [Id] = 3)
        INSERT INTO [purchase].[PurchaseType] ([Id], [Name]) VALUES (3, 'Expense');
END
GO

-- Step 3: Add PurchaseTypeId column to [purchase].[Purchase] if not already present
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'Purchase'
      AND COLUMN_NAME = 'PurchaseTypeId'
)
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD [PurchaseTypeId] INT NOT NULL
            CONSTRAINT [DF_Purchase_PurchaseTypeId] DEFAULT (3);
END
GO

-- Step 4: Add FK constraint from Purchase to PurchaseType
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_Purchase_PurchaseType'
      AND [parent_object_id] = OBJECT_ID('[purchase].[Purchase]')
)
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD CONSTRAINT [FK_Purchase_PurchaseType]
            FOREIGN KEY ([PurchaseTypeId])
            REFERENCES [purchase].[PurchaseType] ([Id]);
END
GO
