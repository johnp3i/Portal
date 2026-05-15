/*
    Migration: 005_CreateQuotationStatusTypeTable
    Description: Creates the [quotation].QuotationStatusType reference table — a system-wide
                 lookup table defining the lifecycle states of a Quotation.
                 This is a shared reference table with no BusinessId column.

    Requirements: 4.3 - THE Portal_Database SHALL contain a [quotation].QuotationStatusType
                         reference table with columns: Id (PK, int), Name (nvarchar, required)
                         seeded with values: Draft (1), Sent (2), Accepted (3),
                         Converted (4), Archived (5)

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'QuotationStatusType'
)
BEGIN
    CREATE TABLE [quotation].[QuotationStatusType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_QuotationStatusType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Seed data: idempotent inserts using MERGE
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'QuotationStatusType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationStatusType] WHERE [Id] = 1)
        INSERT INTO [quotation].[QuotationStatusType] ([Id], [Name]) VALUES (1, 'Draft');

    IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationStatusType] WHERE [Id] = 2)
        INSERT INTO [quotation].[QuotationStatusType] ([Id], [Name]) VALUES (2, 'Sent');

    IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationStatusType] WHERE [Id] = 3)
        INSERT INTO [quotation].[QuotationStatusType] ([Id], [Name]) VALUES (3, 'Accepted');

    IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationStatusType] WHERE [Id] = 4)
        INSERT INTO [quotation].[QuotationStatusType] ([Id], [Name]) VALUES (4, 'Converted');

    IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationStatusType] WHERE [Id] = 5)
        INSERT INTO [quotation].[QuotationStatusType] ([Id], [Name]) VALUES (5, 'Archived');
END
GO
