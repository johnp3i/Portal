/*
    Migration: 045_CreatePurchaseOriginTypeTable
    Description: Creates the [purchase].[PurchaseOriginType] lookup table — a system-wide
                 reference table defining the geographic origin classification of a Purchase.
                 This is a shared reference table with no BusinessId column.
                 
                 Also migrates the [purchase].[Purchase] table from the boolean
                 IsEuReverseCharge column to a PurchaseOriginTypeId FK column,
                 preserving existing data (IsEuReverseCharge=1 maps to EuReverseCharge=2,
                 IsEuReverseCharge=0 maps to Domestic=1).

    Requirements: 7.1 - THE system SHALL provide a [purchase].[PurchaseOriginType] lookup
                         table with three entries: Domestic (Id=1), EuReverseCharge (Id=2),
                         NonEu (Id=3).

    This script is idempotent — safe to run multiple times.
*/

-- Step 1: Create the PurchaseOriginType lookup table
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'PurchaseOriginType'
)
BEGIN
    CREATE TABLE [purchase].[PurchaseOriginType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_PurchaseOriginType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Step 2: Seed data — idempotent inserts
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'PurchaseOriginType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [purchase].[PurchaseOriginType] WHERE [Id] = 1)
        INSERT INTO [purchase].[PurchaseOriginType] ([Id], [Name]) VALUES (1, 'Domestic');

    IF NOT EXISTS (SELECT 1 FROM [purchase].[PurchaseOriginType] WHERE [Id] = 2)
        INSERT INTO [purchase].[PurchaseOriginType] ([Id], [Name]) VALUES (2, 'EuReverseCharge');

    IF NOT EXISTS (SELECT 1 FROM [purchase].[PurchaseOriginType] WHERE [Id] = 3)
        INSERT INTO [purchase].[PurchaseOriginType] ([Id], [Name]) VALUES (3, 'NonEu');
END
GO

-- Step 3: Add PurchaseOriginTypeId column to [purchase].[Purchase] if not already present
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'Purchase'
      AND COLUMN_NAME = 'PurchaseOriginTypeId'
)
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD [PurchaseOriginTypeId] INT NOT NULL
            CONSTRAINT [DF_Purchase_PurchaseOriginTypeId] DEFAULT (1);
END
GO

-- Step 4: Migrate existing data — map IsEuReverseCharge=1 to PurchaseOriginTypeId=2
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'Purchase'
      AND COLUMN_NAME = 'IsEuReverseCharge'
)
BEGIN
    UPDATE [purchase].[Purchase]
        SET [PurchaseOriginTypeId] = 2
        WHERE [IsEuReverseCharge] = 1;
END
GO

-- Step 5: Add FK constraint from Purchase to PurchaseOriginType
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_Purchase_PurchaseOriginType'
      AND [parent_object_id] = OBJECT_ID('[purchase].[Purchase]')
)
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD CONSTRAINT [FK_Purchase_PurchaseOriginType]
            FOREIGN KEY ([PurchaseOriginTypeId])
            REFERENCES [purchase].[PurchaseOriginType] ([Id]);
END
GO

-- Step 6: Drop the old IsEuReverseCharge column and its default constraint
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'Purchase'
      AND COLUMN_NAME = 'IsEuReverseCharge'
)
BEGIN
    -- Drop the default constraint first
    IF EXISTS (
        SELECT 1
        FROM sys.default_constraints
        WHERE [name] = 'DF_Purchase_IsEuReverseCharge'
          AND [parent_object_id] = OBJECT_ID('[purchase].[Purchase]')
    )
    BEGIN
        ALTER TABLE [purchase].[Purchase]
            DROP CONSTRAINT [DF_Purchase_IsEuReverseCharge];
    END

    -- Drop the column
    ALTER TABLE [purchase].[Purchase]
        DROP COLUMN [IsEuReverseCharge];
END
GO
