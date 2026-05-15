/*
    Migration: 038_CreateInvoiceSectionTable
    Description: Creates the [invoice].[InvoiceSection] table to support section-based
                 invoice presentation. Sections group invoice lines and carry rendering
                 metadata (column configuration, emphasis, accent color, labels, totals).
                 Also adds a foreign key from [invoice].[InvoiceLine].[InvoiceSectionId]
                 to [invoice].[InvoiceSection].[Id].

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice' AND TABLE_NAME = 'InvoiceSection'
)
BEGIN
    CREATE TABLE [invoice].[InvoiceSection]
    (
        [Id]                    INT            IDENTITY(1,1) NOT NULL,
        [InvoiceId]             INT                          NOT NULL,
        [Name]                  NVARCHAR(200)                NOT NULL,
        [SortOrder]             INT                          NOT NULL,
        [ColumnConfiguration]   NVARCHAR(50)                 NOT NULL CONSTRAINT [DF_InvoiceSection_ColumnConfiguration] DEFAULT ('OneTime'),
        [SectionType]           NVARCHAR(20)                 NOT NULL CONSTRAINT [DF_InvoiceSection_SectionType] DEFAULT ('LineItems'),
        [Description]           NVARCHAR(MAX)                NULL,
        [Notes]                 NVARCHAR(MAX)                NULL,
        [IsEmphasized]          BIT                          NOT NULL CONSTRAINT [DF_InvoiceSection_IsEmphasized] DEFAULT (0),
        [AccentColor]           NVARCHAR(20)                 NULL,
        [Label]                 NVARCHAR(100)                NULL,
        [IsTotalsTableShown]    BIT                          NOT NULL CONSTRAINT [DF_InvoiceSection_IsTotalsTableShown] DEFAULT (0),

        CONSTRAINT [PK_InvoiceSection] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_InvoiceSection_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id]) ON DELETE CASCADE
    );
END
GO

-- Add FK from InvoiceLine to InvoiceSection (after InvoiceSection table exists)
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = 'FK_InvoiceLine_InvoiceSection'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
    ADD CONSTRAINT [FK_InvoiceLine_InvoiceSection]
        FOREIGN KEY ([InvoiceSectionId]) REFERENCES [invoice].[InvoiceSection] ([Id]);
END
GO
