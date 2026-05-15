/*
    Migration: 014_CreateSupplierTable
    Description: Creates the [purchase].Supplier table — a vendor entity from whom
                 Purchases are made. Scoped to a Business tenant with a foreign key
                 to [portal].Business.

    Requirements: 7.2 - THE Portal_Database SHALL contain a [purchase].Supplier table
                         with columns: Id (PK, int identity), BusinessId (FK to
                         [portal].Business), Name (nvarchar, required), IsActive
                         (bit, default 1), CreatedAtUtc (datetime2)

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'Supplier'
)
BEGIN
    CREATE TABLE [purchase].[Supplier]
    (
        [Id]            INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]    INT                           NOT NULL,
        [Name]          NVARCHAR(200)                 NOT NULL,
        [IsActive]      BIT                           NOT NULL  CONSTRAINT [DF_Supplier_IsActive] DEFAULT (1),
        [CreatedAtUtc]  DATETIME2                     NOT NULL  CONSTRAINT [DF_Supplier_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Supplier] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Supplier_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Supplier_BusinessId'
      AND [object_id] = OBJECT_ID('[purchase].[Supplier]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Supplier_BusinessId]
        ON [purchase].[Supplier] ([BusinessId]);
END
GO
