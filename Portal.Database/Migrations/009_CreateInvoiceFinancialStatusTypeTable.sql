/*
    Migration: 009_CreateInvoiceFinancialStatusTypeTable
    Description: Creates the [invoice].InvoiceFinancialStatusType reference table — a system-wide
                 lookup table defining the financial states of an Invoice.
                 This is a shared reference table with no BusinessId column.

    Requirements: 5.4 - THE Portal_Database SHALL contain an [invoice].InvoiceFinancialStatusType
                         reference table with columns: Id (PK, int), Name (nvarchar, required)
                         seeded with values: Unpaid (1), PartiallyPaid (2), Paid (3),
                         Overdue (4), WrittenOff (5)

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceFinancialStatusType'
)
BEGIN
    CREATE TABLE [invoice].[InvoiceFinancialStatusType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_InvoiceFinancialStatusType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceFinancialStatusType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceFinancialStatusType] WHERE [Id] = 1)
        INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name]) VALUES (1, 'Unpaid');

    IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceFinancialStatusType] WHERE [Id] = 2)
        INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name]) VALUES (2, 'PartiallyPaid');

    IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceFinancialStatusType] WHERE [Id] = 3)
        INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name]) VALUES (3, 'Paid');

    IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceFinancialStatusType] WHERE [Id] = 4)
        INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name]) VALUES (4, 'Overdue');

    IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceFinancialStatusType] WHERE [Id] = 5)
        INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name]) VALUES (5, 'WrittenOff');
END
GO
