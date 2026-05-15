/*
    Migration: 016_CreatePurchaseTable
    Description: Creates the [purchase].Purchase table — an expense entry representing
                 money spent by the Business, with VAT tracking. Scoped to a Business
                 tenant with foreign keys to [portal].Business, [purchase].Supplier,
                 and [purchase].ExpenseCategory.

    Requirements: 7.1 - THE Portal_Database SHALL contain a [purchase].Purchase table
                         with columns: Id (PK, int identity), BusinessId (FK to
                         [portal].Business), SupplierId (FK to [purchase].Supplier),
                         ExpenseCategoryId (FK to [purchase].ExpenseCategory),
                         InvoiceNumber (nvarchar, nullable), InvoiceDate (date),
                         Description (nvarchar, required), AmountExcludingVat
                         (decimal(18,2)), VatAmount (decimal(18,2)), TotalAmount
                         (decimal(18,2)), IsEuReverseCharge (bit, default 0),
                         Country (nvarchar, nullable), Notes (nvarchar(max), nullable),
                         CreatedAtUtc (datetime2), UpdatedAtUtc (datetime2)
                 7.4 - WHEN IsEuReverseCharge is set to 1, THE Portal_Database SHALL
                         allow VatAmount to be 0 (no CHECK constraint preventing zero
                         VAT on reverse charge entries)

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'Purchase'
)
BEGIN
    CREATE TABLE [purchase].[Purchase]
    (
        [Id]                    INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]            INT                           NOT NULL,
        [SupplierId]            INT                           NOT NULL,
        [ExpenseCategoryId]     INT                           NOT NULL,
        [InvoiceNumber]         NVARCHAR(100)                 NULL,
        [InvoiceDate]           DATE                          NOT NULL,
        [Description]           NVARCHAR(500)                 NOT NULL,
        [AmountExcludingVat]    DECIMAL(18,2)                 NOT NULL,
        [VatAmount]             DECIMAL(18,2)                 NOT NULL,
        [TotalAmount]           DECIMAL(18,2)                 NOT NULL,
        [IsEuReverseCharge]     BIT                           NOT NULL  CONSTRAINT [DF_Purchase_IsEuReverseCharge] DEFAULT (0),
        [Country]               NVARCHAR(100)                 NULL,
        [Notes]                 NVARCHAR(MAX)                 NULL,
        [CreatedAtUtc]          DATETIME2                     NOT NULL  CONSTRAINT [DF_Purchase_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]          DATETIME2                     NOT NULL  CONSTRAINT [DF_Purchase_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Purchase] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Purchase_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_Purchase_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [purchase].[Supplier] ([Id]),
        CONSTRAINT [FK_Purchase_ExpenseCategory] FOREIGN KEY ([ExpenseCategoryId]) REFERENCES [purchase].[ExpenseCategory] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Purchase_BusinessId'
      AND [object_id] = OBJECT_ID('[purchase].[Purchase]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Purchase_BusinessId]
        ON [purchase].[Purchase] ([BusinessId]);
END
GO
