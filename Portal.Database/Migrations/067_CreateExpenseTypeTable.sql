/*
    Migration: 067_CreateExpenseTypeTable
    Description: Creates the [purchase].[ExpenseType] lookup table — a system-wide
                 reference table classifying whether an expense category relates to
                 Services or Goods.
                 
                 Also adds a nullable ExpenseTypeId FK column to [purchase].[ExpenseCategory]
                 to associate each category with an expense type. Nullable to allow
                 backward compatibility with legacy categories created before this feature.

    Requirements: 2.1 - THE Portal_System SHALL provide an ExpenseType lookup table with
                         two entries: Services (Id=1) and Goods (Id=2)
                  2.2 - THE Portal_System SHALL store a nullable ExpenseTypeId foreign key
                         on the ExpenseCategory table referencing the ExpenseType lookup

    This script is idempotent — safe to run multiple times.
*/

-- Step 1: Create the ExpenseType lookup table
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'ExpenseType'
)
BEGIN
    CREATE TABLE [purchase].[ExpenseType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_ExpenseType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Step 2: Seed data — idempotent inserts
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'ExpenseType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [purchase].[ExpenseType] WHERE [Id] = 1)
        INSERT INTO [purchase].[ExpenseType] ([Id], [Name]) VALUES (1, 'Services');

    IF NOT EXISTS (SELECT 1 FROM [purchase].[ExpenseType] WHERE [Id] = 2)
        INSERT INTO [purchase].[ExpenseType] ([Id], [Name]) VALUES (2, 'Goods');
END
GO

-- Step 3: Add ExpenseTypeId column to [purchase].[ExpenseCategory] if not already present
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'ExpenseCategory'
      AND COLUMN_NAME = 'ExpenseTypeId'
)
BEGIN
    ALTER TABLE [purchase].[ExpenseCategory]
        ADD [ExpenseTypeId] INT NULL;
END
GO

-- Step 4: Add FK constraint from ExpenseCategory to ExpenseType
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_ExpenseCategory_ExpenseType'
      AND [parent_object_id] = OBJECT_ID('[purchase].[ExpenseCategory]')
)
BEGIN
    ALTER TABLE [purchase].[ExpenseCategory]
        ADD CONSTRAINT [FK_ExpenseCategory_ExpenseType]
            FOREIGN KEY ([ExpenseTypeId])
            REFERENCES [purchase].[ExpenseType] ([Id]);
END
GO
