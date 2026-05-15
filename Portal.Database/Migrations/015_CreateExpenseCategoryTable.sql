/*
    Migration: 015_CreateExpenseCategoryTable
    Description: Creates the [purchase].ExpenseCategory table — a classification
                 for Purchase entries. Scoped to a Business tenant with a foreign key
                 to [portal].Business.

    Requirements: 7.3 - THE Portal_Database SHALL contain a [purchase].ExpenseCategory
                         table with columns: Id (PK, int identity), BusinessId (FK to
                         [portal].Business), Name (nvarchar, required), IsActive
                         (bit, default 1)

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'ExpenseCategory'
)
BEGIN
    CREATE TABLE [purchase].[ExpenseCategory]
    (
        [Id]            INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]    INT                           NOT NULL,
        [Name]          NVARCHAR(100)                 NOT NULL,
        [IsActive]      BIT                           NOT NULL  CONSTRAINT [DF_ExpenseCategory_IsActive] DEFAULT (1),

        CONSTRAINT [PK_ExpenseCategory] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ExpenseCategory_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_ExpenseCategory_BusinessId'
      AND [object_id] = OBJECT_ID('[purchase].[ExpenseCategory]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExpenseCategory_BusinessId]
        ON [purchase].[ExpenseCategory] ([BusinessId]);
END
GO
