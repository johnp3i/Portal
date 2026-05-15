/*
    Migration: 008_CreateInvoiceStatusTypeTable
    Description: Creates the [invoice].InvoiceStatusType reference table — a system-wide
                 lookup table defining the document lifecycle states of an Invoice.
                 This is a shared reference table with no BusinessId column.

    Requirements: 5.3 - THE Portal_Database SHALL contain an [invoice].InvoiceStatusType
                         reference table with columns: Id (PK, int), Name (nvarchar, required)
                         seeded with values: Draft (1), Issued (2), Cancelled (3)

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceStatusType'
)
BEGIN
    CREATE TABLE [invoice].[InvoiceStatusType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_InvoiceStatusType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceStatusType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceStatusType] WHERE [Id] = 1)
        INSERT INTO [invoice].[InvoiceStatusType] ([Id], [Name]) VALUES (1, 'Draft');

    IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceStatusType] WHERE [Id] = 2)
        INSERT INTO [invoice].[InvoiceStatusType] ([Id], [Name]) VALUES (2, 'Issued');

    IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceStatusType] WHERE [Id] = 3)
        INSERT INTO [invoice].[InvoiceStatusType] ([Id], [Name]) VALUES (3, 'Cancelled');
END
GO
