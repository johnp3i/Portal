/*
    Migration: 004_CreateCustomerTable
    Description: Creates the [customer].Customer table — a client entity registered
                 under a specific Business tenant for associating quotations and invoices.

    Requirements: 3.1 - THE Portal_Database SHALL contain a [customer].Customer table with columns:
                         Id (PK, int identity), BusinessId (FK to [portal].Business), Name (nvarchar, required),
                         Email (nvarchar, nullable), TelephoneNumber (nvarchar, nullable),
                         AddressLine1 (nvarchar, nullable), AddressLine2 (nvarchar, nullable),
                         City (nvarchar, nullable), PostalCode (nvarchar, nullable),
                         Country (nvarchar, nullable), IsActive (bit, default 1),
                         CreatedAtUtc (datetime2), UpdatedAtUtc (datetime2)
                  3.2 - THE Portal_Database SHALL enforce that [customer].Customer.BusinessId
                         references [portal].Business.Id via a foreign key constraint

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'customer'
      AND TABLE_NAME = 'Customer'
)
BEGIN
    CREATE TABLE [customer].[Customer]
    (
        [Id]               INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]       INT                           NOT NULL,
        [Name]             NVARCHAR(200)                 NOT NULL,
        [Email]            NVARCHAR(200)                 NULL,
        [TelephoneNumber]  NVARCHAR(30)                  NULL,
        [AddressLine1]     NVARCHAR(200)                 NULL,
        [AddressLine2]     NVARCHAR(200)                 NULL,
        [City]             NVARCHAR(100)                 NULL,
        [PostalCode]       NVARCHAR(20)                  NULL,
        [Country]          NVARCHAR(100)                 NULL,
        [IsActive]         BIT                           NOT NULL  CONSTRAINT [DF_Customer_IsActive] DEFAULT (1),
        [CreatedAtUtc]     DATETIME2                     NOT NULL  CONSTRAINT [DF_Customer_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]     DATETIME2                     NOT NULL  CONSTRAINT [DF_Customer_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Customer] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Customer_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Customer_BusinessId'
      AND [object_id] = OBJECT_ID('[customer].[Customer]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Customer_BusinessId]
        ON [customer].[Customer] ([BusinessId]);
END
GO
